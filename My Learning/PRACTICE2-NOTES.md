# Practice2 notes: the API, step by step

The second rebuild of the API, in `practice2/`. One section per step: the commands, the code, and why
each part is there. The step list is in [PRACTICE-PLAN.md](PRACTICE-PLAN.md); the concepts are in
[DOTNET-CONCEPTS.md](DOTNET-CONCEPTS.md) (referred to as "concept N").

| Step | What | Status |
|---|---|---|
| 1 | Empty Web API with controllers, a solution, a hard-coded endpoint | ✅ |
| 2 | `Company` entity, `AppDbContext`, first migration | ✅ |
| 3 | Read from the database with a DTO | ✅ |
| 4 | Search with filters and paging | ✅ |
| 5 | MOD11 validation with xUnit tests | ✅ |
| 6 | Typed Brreg client with resilience | ✅ |
| 7 | Importer: validate, then upsert page by page | ✅ |
| 8 | Queue, background worker, `import_runs`, Abandoned on startup | ✅ |

---

## Step 1: an empty Web API

```bash
cd practice2
dotnet new sln -n Practice2                            # Practice2.slnx: a list of projects
dotnet new webapi -n Practice2.Api --use-controllers   # the API project, with a Controllers folder
dotnet sln add Practice2.Api                           # put the project in the solution
```

- **Solution (`.slnx`) vs project (`.csproj`):** the project is one thing that builds (the API, later the
  tests). The solution only lists projects, so the editor and `dotnet build` see them together. Like a
  monorepo's root `package.json` workspaces list.
- `--use-controllers` gives controller classes instead of minimal APIs (`app.MapGet(...)` in Program.cs).
- Removed the WeatherForecast sample. Port set to **5020** in `Properties/launchSettings.json` (the `http`
  profile), so it doesn't clash with `practice/` on 5010.
- VS Code asked which solution to open: `"dotnet.defaultSolution": "practice2/Practice2.slnx"` in
  `.vscode/settings.json` answers it once.

The first endpoint used a hard-coded `List<CompanyDto>`, to see routing and JSON before any database.

## Step 2: entity, DbContext, migration

**Packages** (run in `Practice2.Api/`):
```bash
dotnet add package Microsoft.EntityFrameworkCore --version 10.0.13
dotnet add package Microsoft.EntityFrameworkCore.Relational --version 10.0.13
dotnet add package Microsoft.EntityFrameworkCore.Design --version 10.0.13   # needed by the dotnet-ef tool
dotnet add package Npgsql.EntityFrameworkCore.PostgreSQL --version 10.0.3   # the PostgreSQL provider
dotnet add package EFCore.NamingConventions --version 10.0.1                # snake_case table and column names
```

**`Data/Company.cs`, the entity:** one class = one table, one property = one column. Just data.
```csharp
public class Company
{
    public required string Orgnr { get; set; }
    public required string Name { get; set; }
    public required string OrgForm { get; set; }
    public DateOnly? RegistrationDate { get; set; }
}
```
- **Properties** (`{ get; set; }`), not fields: EF maps properties.
- **`required`:** a non-null string that must be set when you create the object. Fixes the nullable
  warning (concept 7). `DateOnly?` is nullable, so the column allows NULL.

**`Data/AppDbContext.cs`, the database session:**
```csharp
public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Company> Companies => Set<Company>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Company>(company =>
        {
            company.HasKey(c => c.Orgnr);
            company.Property(c => c.Orgnr).HasMaxLength(9).IsFixedLength();   // char(9)
            company.Property(c => c.Name).HasMaxLength(500);
            company.Property(c => c.OrgForm).HasMaxLength(10);
        });
    }
}
```
- **The direction:** the DbContext holds `DbSet<Company>`; the controller gets the DbContext injected. The
  entity knows nothing about the database.
- `DbSet<Company>` is the table you query: `db.Companies.Where(...)`.
- `OnModelCreating` is the schema: key, lengths. Without `HasKey`, EF looks for a property called `Id`.

**Register it in `Program.cs`** (concept 1, DI):
```csharp
builder.Services.AddDbContext<AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("Practice2"))
           .UseSnakeCaseNamingConvention());
```
`AddDbContext` registers it as **scoped**: one DbContext per HTTP request.

**`appsettings.Development.json`:**
```json
"ConnectionStrings": {
  "Practice2": "Host=localhost;Port=5433;Database=practice2;Username=companies;Password=companies"
}
```
Plus `"Microsoft.EntityFrameworkCore.Database.Command": "Information"` under `LogLevel`, so the terminal
shows the SQL EF sends. Port 5433 is this repo's Docker Postgres (5432 belongs to another project).

**The migration:**
```bash
dotnet ef migrations add InitialCreate -o Data/Migrations   # -o = --output-dir
dotnet ef database update                                   # creates the database and the table
```
- `migrations add` compares the model with the last snapshot and writes the C# for the difference
  (`Up` and `Down`). `database update` runs it and records it in `__EFMigrationsHistory`.
- The red **`fail`** line on the first `database update` is expected: EF tries to connect, the database
  doesn't exist yet, so it runs `CREATE DATABASE`. The "exclusive lock" lines stop two processes
  migrating at the same time.
- The browser opening `api.nuget.org/v3-vulnerabilities/...` was NuGet Audit's vulnerability list (the
  `npm audit` of .NET), downloaded on restore. Harmless.

## Step 3: read from the database

**Real data:** copied the 7,211 companies from the `practice` database (repo root):
```bash
docker compose exec -T db psql -U companies -d practice -c "\copy (SELECT orgnr, name, org_form, registration_date FROM companies) TO STDOUT" \
  | docker compose exec -T db psql -U companies -d practice2 -c "\copy companies FROM STDIN"
```
`\copy ... TO STDOUT` writes rows as text, `|` pipes them, `\copy ... FROM STDIN` reads them in.

**`Contracts/CompanyDto.cs`, what the API returns:**
```csharp
public record CompanyDto(string Orgnr, string Name, string OrgForm, DateOnly? RegistrationDate);
```

**The controller:**
```csharp
[ApiController]
[Route("api/companies")]
public class CompaniesController(AppDbContext db) : ControllerBase
{
    [HttpGet("{orgnr}")]
    public async Task<ActionResult<CompanyDto>> GetByOrgnr(string orgnr, CancellationToken ct)
    {
        var company = await db.Companies
            .AsNoTracking()
            .Where(c => c.Orgnr == orgnr)
            .Select(c => new CompanyDto(c.Orgnr, c.Name, c.OrgForm, c.RegistrationDate))
            .FirstOrDefaultAsync(ct);

        return company is null ? NotFound() : company;
    }
}
```
| Part | Why | Concept |
|---|---|---|
| `CompaniesController(AppDbContext db)` | DI passes a fresh, scoped DbContext per request | 1 |
| `async` / `await` / `CancellationToken ct` | the thread is free while the database works; a closed request cancels the query | 3 |
| `AsNoTracking()` | a read: no change-tracking snapshot | 4 |
| `Where` first, `Select` last | EF sends only the 4 columns, and the entity never leaves the API | 5, 7 |
| `ActionResult<CompanyDto>` | can return either the DTO (200) or `NotFound()` (404) | 8 |

## Step 4: search with filters and paging

**`Contracts/CompanySearchQuery.cs`, the query string as a class:**
```csharp
public record CompanySearchQuery
{
    public string? Name { get; set; }
    public string? OrgForm { get; set; }

    [Range(1, 1000)]
    public int Page { get; set; } = 1;

    [Range(1, 100)]
    public int PageSize { get; set; } = 20;
}
```
- Instead of four method parameters, one object. `[FromQuery]` fills it from `?name=bil&page=2`, like
  `$this->input->get()` but typed.
- `string?` = optional filter. `= 1` / `= 20` = the default when the URL leaves it out.
- `[Range]` is checked by `[ApiController]` before the method runs: `?pageSize=500` gets an automatic
  **400**, so nobody can download the whole table in one request.

**`Contracts/PageResult.cs`, the paged answer:**
```csharp
public record PageResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount);
```
`<T>` is a generic: `PageResult<CompanyDto>` now, `PageResult<RoleDto>` in step 9d.

**The search:**
```csharp
[HttpGet]
public async Task<PageResult<CompanyDto>> Search([FromQuery] CompanySearchQuery query, CancellationToken ct)
{
    IQueryable<Company> companies = db.Companies.AsNoTracking();

    if (!string.IsNullOrWhiteSpace(query.Name))
    {
        companies = companies.Where(c => EF.Functions.ILike(c.Name, $"%{query.Name}%"));
    }
    if (!string.IsNullOrWhiteSpace(query.OrgForm))
    {
        var orgForm = query.OrgForm.Trim().ToUpperInvariant();
        companies = companies.Where(c => c.OrgForm == orgForm);
    }

    var total = await companies.CountAsync(ct);
    var items = await companies
        .OrderBy(c => c.Name)
        .Skip((query.Page - 1) * query.PageSize)
        .Take(query.PageSize)
        .Select(c => new CompanyDto(c.Orgnr, c.Name, c.OrgForm, c.RegistrationDate))
        .ToListAsync(ct);

    return new PageResult<CompanyDto>(items, query.Page, query.PageSize, total);
}
```
- **`IQueryable` (concept 5):** nothing runs until `CountAsync` / `ToListAsync`. Each `if` adds a `WHERE`
  to the SQL being built, like CodeIgniter's query builder before `->get()`.
- **`EF.Functions.ILike`** = PostgreSQL's case-insensitive `ILIKE '%bil%'`, sent as a parameter (no SQL
  injection).
- **`ToUpperInvariant()`:** org forms are stored as `AS`, so `?orgForm=as` must be upper-cased first.
- **Two queries:** `SELECT count(*)` with the same filters, then the page with `LIMIT` and `OFFSET`.
- **`OrderBy` before `Skip`:** without a fixed order, a row can show up on two pages.

```bash
curl -sS "localhost:5020/api/companies?name=bil" | jq '{totalCount, page, first: .items[0].name}'
curl -sS "localhost:5020/api/companies?name=bil&orgForm=as&page=2&pageSize=5" | jq '.items | length'   # 5
curl -si "localhost:5020/api/companies?pageSize=500" | head -1                                         # 400
```

## Step 5: MOD11 validation, with xUnit tests

**MOD11:** the 9th digit of an orgnr is a check digit calculated from the first 8, so a typo almost always
breaks it. Example, `912855414`:
1. First 8 digits × weights `3 2 7 6 5 4 3 2`, summed: `9·3 + 1·2 + 2·7 + 8·6 + 5·5 + 5·4 + 4·3 + 1·2 = 150`
2. `150 % 11 = 7`
3. `11 − 7 = 4` = the last digit, so it's valid. If the result is 11, the check digit is 0; if it's 10,
   the number can never be valid.

**The test project:**
```bash
cd practice2
dotnet new xunit -n Practice2.Tests
dotnet sln add Practice2.Tests          # the solution now lists 2 projects
cd Practice2.Tests
dotnet add reference ../Practice2.Api   # the tests can use the API's classes
rm UnitTest1.cs
```

**`Practice2.Api/Domain/Orgnr.cs`:**
```csharp
public static class Orgnr
{
    private static readonly int[] Weights = [3, 2, 7, 6, 5, 4, 3, 2];

    public static bool IsValid(string? value)
    {
        if (value is null || value.Length != 9 || !value.All(char.IsAsciiDigit))
            return false;

        var sum = 0;
        for (var i = 0; i < Weights.Length; i++)
        {
            sum += (value[i] - '0') * Weights[i];
        }

        var control = 11 - (sum % 11);
        if (control == 11)
            control = 0;
        else if (control == 10)
            return false;

        return control == value[8] - '0';
    }
}
```
- `static class`: no state, no DI, a pure function. `Domain/` keeps business rules apart from HTTP and
  the database, which is what makes it easy to test.
- `value[i] - '0'` turns the char `'7'` into the int `7` (chars are numbers underneath).
- `char.IsAsciiDigit`, not `char.IsDigit`: `IsDigit` also accepts digits like Arabic-Indic `٣`.

**`Practice2.Tests/OrgnrTests.cs`:**
```csharp
public class OrgnrTests
{
    [Theory]
    [InlineData("923609016")] // Equinor ASA
    [InlineData("974760673")] // Brønnøysundregistrene
    [InlineData("912855414")]
    public void Accepts_valid_numbers(string value)
    {
        Assert.True(Orgnr.IsValid(value));
    }

    [Theory]
    [InlineData("923609017")] // wrong check digit (Equinor is 923609016)
    [InlineData("12345678")]  // too short
    [InlineData("1234567890")] // too long
    [InlineData("92360901A")] // not a digit
    [InlineData("")]
    [InlineData(null)]
    public void Rejects_invalid_values(string? value)
    {
        Assert.False(Orgnr.IsValid(value));
    }

    [Fact]
    public void Rejects_a_prefix_where_the_check_digit_would_be_10()
    {
        // 4·3 = 12, 12 % 11 = 1, 11 − 1 = 10: no last digit can be valid.
        for (var d = 0; d <= 9; d++)
        {
            Assert.False(Orgnr.IsValid($"00000040{d}"));
        }
    }
}
```
- `[Fact]` = one test, no input. `[Theory]` + `[InlineData]` = one test per row, like PHPUnit's
  `@dataProvider`.
- Each "reject" row breaks exactly one rule (`923609017` keeps 9 characters; only the check digit is
  wrong).
- `$"00000040{d}"` is **string interpolation**, like PHP's `"00000040{$d}"` or JS's `` `00000040${d}` ``.
  The loop tests `000000400` to `000000409`.

**In the controller**, before the query in `GetByOrgnr`:
```csharp
if (!Orgnr.IsValid(orgnr))
{
    return Problem(
        statusCode: StatusCodes.Status400BadRequest,
        title: "Invalid organisation number",
        detail: "An organisation number is nine digits with a valid MOD11 check digit.");
}
```
`Problem(...)` returns RFC 9457 problem details, the same JSON shape as every other ASP.NET error.

```bash
dotnet test                                                    # Passed: 10
curl -si localhost:5020/api/companies/123 | head -1            # 400: not an orgnr
curl -si localhost:5020/api/companies/912855414 | head -1      # 200
curl -si localhost:5020/api/companies/923609016 | head -1      # 404: valid orgnr, not in our data
```

## Step 6: a typed Brreg client, with resilience

```bash
dotnet add package Microsoft.Extensions.Http.Resilience --version 10.11.0
```

**`Brreg/BrregModels.cs`, records shaped like Brreg's JSON:**
```csharp
public record BrregPage(
    [property: JsonPropertyName("_embedded")] BrregEmbedded? Embedded,
    BrregPageInfo Page);

public record BrregEmbedded(IReadOnlyList<BrregEnhet> Enheter);

public record BrregPageInfo(int Size, int TotalElements, int TotalPages, int Number);

public record BrregEnhet(
    string? Organisasjonsnummer,
    string? Navn,
    BrregOrgForm? Organisasjonsform,
    DateOnly? RegistreringsdatoEnhetsregisteret);

public record BrregOrgForm(string? Kode);
```
- Mirrors `{ "_embedded": { "enheter": [...] }, "page": {...} }`. Fields without a property are skipped:
  e-mail and phone are never even read (data minimisation).
- Names match case-insensitively; only `_embedded` needs `[JsonPropertyName]`. `[property: ...]` puts the
  attribute on the generated property, not the constructor parameter.
- Everything `?`: someone else's data, expect gaps. Step 7 validates before saving.
- **Why not `Data/`:** `Data/` is *our* database (entities + DbContext). These records are
  Brreg's wire format, the outside world's contract. Same reason `Contracts/` is separate: our API's
  shape. (Written in `Brreg/` first; moved to `Import/` in step 7.)

**`Brreg/BrregClient.cs`:**
```csharp
public class BrregClient(HttpClient http)
{
    public async Task<BrregPage> GetPageAsync(int page, int size, CancellationToken ct)
    {
        // Horten (3901) and Tønsberg (3905), AS and ASA.
        var url = $"enhetsregisteret/api/enheter?page={page}&size={size}&kommunenummer=3901,3905&organisasjonsform=AS,ASA";

        return await http.GetFromJsonAsync<BrregPage>(url, ct)
            ?? throw new InvalidOperationException($"Brreg returned an empty body for page {page}.");
    }
}
```
`GetFromJsonAsync<T>` = GET + status check + deserialise, like Nuxt's `$fetch<T>()`. The URL is relative
to the base address.

**`Program.cs`:**
```csharp
builder.Services.AddHttpClient<BrregClient>(http =>
{
    http.BaseAddress = new Uri("https://data.brreg.no/");   // trailing / matters
    http.DefaultRequestHeaders.UserAgent.ParseAdd("practice2-api/1.0");
})
.AddStandardResilienceHandler();
```
- **Typed client:** DI builds `BrregClient` with a configured `HttpClient`.
- **`.AddStandardResilienceHandler()`:** retries with growing waits (5xx, 429, timeouts), a timeout per try
  and in total, and a circuit breaker (stop calling a service that keeps failing).

**Why not `new HttpClient()` per call?** Disposing it *does* close the connection, but the operating system
keeps the closed socket in `TIME_WAIT` for up to a few minutes, so its port can't be reused yet. Under
load, a new client per request uses up ports faster than they're freed: **socket exhaustion**. Also
every call pays a new TCP + TLS handshake. The opposite, one static `HttpClient` forever, keeps
connections open so long it never notices a DNS change. `AddHttpClient` (IHttpClientFactory) solves
both: it pools the connection handlers and recycles them every couple of minutes.

**Temporary test endpoint** (in `CompaniesController`, removed in step 7):
```csharp
[HttpGet("brreg")]
public async Task<BrregPage> GetBrreg(BrregClient brreg, [FromQuery] int page, [FromQuery] int size, CancellationToken ct)
    => await brreg.GetPageAsync(page, size, ct);
```
`BrregClient` here is an action parameter filled from DI: `[ApiController]` sees it's a registered service.
The literal route `brreg` wins over `{orgnr}`, so the two don't clash.

```bash
curl -sS "localhost:5020/api/companies/brreg?page=0&size=3" | jq '.page, [._embedded.enheter[].navn]'
```

## Step 7: the importer, validate then upsert page by page

**Folder:** `BrregClient.cs` and `BrregModel.cs` moved from `Brreg/` to `Import/` (namespace
`Practice2.Api.Import`), next to the importer: grouped by feature. Remember to update the `using` in
`Program.cs` when a namespace changes.

**The three shapes of one company:**

| Shape | Folder | Whose | Used for |
|---|---|---|---|
| `BrregEnhet` | `Import/` | Brreg's | the JSON coming in (`navn`, `organisasjonsnummer`) |
| `Company` | `Data/` | ours: the database | the table row EF reads and saves |
| `CompanyDto` | `Contracts/` | ours: the API | the JSON going out to the frontend |

The importer maps `BrregEnhet` → `Company`; the controller's `Select` maps `Company` → `CompanyDto`. Each
side can change alone.

**One page, in words:** fetch → validate (Domain rules, on the Brreg record) → one query for the existing
rows → update or add (tracking notices changes) → save once per page.

**`Import/CompanyImporter.cs`:**
```csharp
public record ImportSummary(int Read, int Inserted, int Updated, int Unchanged, int Rejected);

public class CompanyImporter(AppDbContext db, BrregClient brreg, ILogger<CompanyImporter> logger)
{
    private const int PageSize = 500;

    public async Task<ImportSummary> RunAsync(CancellationToken ct)
    {
        int read = 0, inserted = 0, updated = 0, unchanged = 0, rejected = 0;

        for (var page = 0; ; page++)
        {
            // Brreg's search API refuses to page past 10,000 rows.
            if ((page + 1) * PageSize > 10_000)
                break;

            var result = await brreg.GetPageAsync(page, PageSize, ct);
            var units = result.Embedded?.Enheter ?? [];
            read += units.Count;

            // 1. Validate: only clean rows go further.
            var validBrregList = new List<BrregEnhet>();
            foreach (var unit in units)
            {
                if (Orgnr.IsValid(unit.Organisasjonsnummer)
                    && !string.IsNullOrWhiteSpace(unit.Navn)
                    && !string.IsNullOrWhiteSpace(unit.Organisasjonsform?.Kode))
                {
                    validBrregList.Add(unit);
                }
                else
                {
                    rejected++;
                    logger.LogWarning("Rejected {Orgnr} ({Name})", unit.Organisasjonsnummer, unit.Navn);
                }
            }

            // 2. Load this page's companies that already exist, in one query.
            var numbers = validBrregList.Select(u => u.Organisasjonsnummer!).ToList();
            var existing = await db.Companies
                .Where(c => numbers.Contains(c.Orgnr))
                .ToDictionaryAsync(c => c.Orgnr, ct);

            // 3. Upsert: update the existing, insert the new.
            foreach (var validBrreg in validBrregList)
            {
                if (existing.TryGetValue(validBrreg.Organisasjonsnummer!, out var existingCompany))
                {
                    existingCompany.Name = validBrreg.Navn!;
                    existingCompany.OrgForm = validBrreg.Organisasjonsform!.Kode!;
                    existingCompany.RegistrationDate = validBrreg.RegistreringsdatoEnhetsregisteret;

                    if (db.Entry(existingCompany).State == EntityState.Modified)
                        updated++;
                    else
                        unchanged++;
                }
                else
                {
                    db.Companies.Add(new Company
                    {
                        Orgnr = validBrreg.Organisasjonsnummer!,
                        Name = validBrreg.Navn!,
                        OrgForm = validBrreg.Organisasjonsform!.Kode!,
                        RegistrationDate = validBrreg.RegistreringsdatoEnhetsregisteret
                    });
                    inserted++;
                }
            }

            // 4. Save this page in one transaction, then forget the tracked objects.
            await db.SaveChangesAsync(ct);
            db.ChangeTracker.Clear();

            logger.LogInformation("Imported page {Page} of {TotalPages}", page + 1, result.Page.TotalPages);

            if (page + 1 >= result.Page.TotalPages)
                break;
        }

        return new ImportSummary(read, inserted, updated, unchanged, rejected);
    }
}
```

**How change tracking works here (concept 4), the part that clicked:**
- The query in step 2 has **no `AsNoTracking()`**. So for each row it loads, EF (1) creates a `Company`
  object and gives it to me, in the dictionary, and (2) keeps a note in `db.ChangeTracker`: "this
  object, state Unchanged, and a snapshot of its original values".
- `existingCompany` from `TryGetValue` is **that same object, not a copy**. `Company` is a class, a
  reference type: the dictionary, my variable and the tracker all point at one object in memory (like
  PHP objects). So setting `existingCompany.Name` changes the object EF is watching.
- A `new Company { ... }` with the same values is a *second* object EF has no note about. Changing it
  saves nothing; `Add`ing it gives a duplicate-key error.
- `db.Entry(x).State` doesn't do the tracking, it *asks* for the verdict: current values vs snapshot →
  `Modified` or `Unchanged`. We only use it to count.
- `SaveChangesAsync` makes the same comparison for every tracked object: `Added` → INSERT, `Modified` →
  UPDATE of the changed columns only, `Unchanged` → nothing. That's why a second run writes nothing.
- `ChangeTracker.Clear()` drops all the notes; the next page starts empty.

**Other details:**
- `numbers.Contains(c.Orgnr)` → `WHERE orgnr = ANY(@numbers)`: one query per page, not 500 (no N+1).
- `ToDictionaryAsync(c => c.Orgnr)` = `Dictionary<string, Company>`, keyed by orgnr. Like building
  `$existing[$c->orgnr] = $c` in PHP.
- `TryGetValue` instead of `existing[orgnr]`: the indexer **throws** `KeyNotFoundException` for a missing
  key (PHP would give null). `TryGetValue` checks and gets in one call.
- `Add` and the property setters are in memory only, so no `await`. Await only what goes to the
  database: queries and `SaveChangesAsync`.
- `unit.Navn!`: the `!` tells the compiler "not null"; the validation already checked.
- `SaveChangesAsync` once per page = one transaction per page. A crash on page 9 keeps pages 1–8.
- `{Orgnr}` placeholders in log messages, not `$"..."`: structured logs keep `Orgnr` as a searchable field.

**`Program.cs`:** `builder.Services.AddScoped<CompanyImporter>();`. Scoped, because it holds the scoped
`AppDbContext`.

**`Controllers/ImportsController.cs`, temporary until step 8:**
```csharp
[ApiController]
[Route("api/imports")]
public class ImportsController(CompanyImporter importer) : ControllerBase
{
    // Temporary: runs the import inside the request. Step 8 moves it to a background worker.
    [HttpPost]
    public async Task<ImportSummary> Run(CancellationToken ct)
        => await importer.RunAsync(ct);
}
```
The `brreg` test endpoint from step 6 was removed from `CompaniesController`.

```bash
curl -sS -X POST localhost:5020/api/imports | jq     # ~7 s; the first run on the copied data inserted 8
curl -sS -X POST localhost:5020/api/imports | jq     # again: inserted 0, everything unchanged
```
The table has more rows than Brreg returns (companies deleted at Brreg stay, because the importer never
deletes). Delete them, or mark them deleted? A design question for later.

**"Address already in use" on port 5020:** an old API process was still running after its terminal was
closed (its parent was `init`). Find it with `ss -ltnp | grep 5020` and stop it with `kill <pid>`. Stop the
API with Ctrl+C before closing a terminal. `dotnet watch --no-hot-reload` restarts on every save.

## Step 8: queue, background worker and `import_runs`

**Why:** `POST /api/imports` kept the request open for the whole import. Now the POST only writes a
"Queued" row and puts its id in a queue, answers **202** at once, and a background worker runs the
import. The client polls `GET /api/imports/{id}`. Like a Laravel `dispatch()` + `queue:work`, but the
queue and the worker live inside the API process.

**`Data/ImportRun.cs`:**
```csharp
public enum ImportStatus
{
    Queued, Running, Succeeded, Failed, Abandoned
}

public class ImportRun
{
    public long Id { get; set; }
    public ImportStatus Status { get; set; }
    public DateTimeOffset QueuedAt { get; set; }
    public DateTimeOffset? StartedAt { get; set; }
    public DateTimeOffset? FinishedAt { get; set; }
    public int Read { get; set; }
    public int Inserted { get; set; }
    public int Updated { get; set; }
    public int Unchanged { get; set; }
    public int Rejected { get; set; }
    public string? Error { get; set; }
}
```
In `AppDbContext`: `public DbSet<ImportRun> ImportRuns => Set<ImportRun>();` and
```csharp
modelBuilder.Entity<ImportRun>(run =>
{
    run.Property(r => r.Status).HasConversion<string>().HasMaxLength(20);
    run.Property(r => r.Error).HasMaxLength(2000);
});
```
- **Enum in C#, text in the database.** The enum lets the compiler catch `ImportStatus.Sucess`, where a
  string `"Sucess"` would quietly be wrong. `HasConversion<string>()` stores `'Running'` instead of `1`:
  readable in psql, and safe if someone reorders the enum.
- `long Id`: a primary key by convention, auto-increment (`GENERATED BY DEFAULT AS IDENTITY`).
- `DateTimeOffset`: a time with its UTC offset → `timestamp with time zone`.
- `?` on `StartedAt` / `FinishedAt`: empty until the run starts / ends.

**Migrations, and fixing one on top:**
```bash
dotnet ef migrations add AddImportRuns -o Data/Migrations
dotnet ef migrations script InitialCreate      # the SQL after InitialCreate: READ IT before updating
dotnet ef database update
```
My first try had a typo (`FinishedT`) and no `?`, so the table got `finishedt ... NOT NULL`. The app then
started and stopped at once: the worker's startup query used `finished_at`, failed, and **a
`BackgroundService` that throws stops the whole app** (the default). Fixed with two migrations on top:
`FixImportRunColumn` (EF detected the rename and generated `RenameColumn`) and
`MakeImportRunTimesNullable` (`AlterColumn ... nullable: true`).
- **Fix on top** is the rule once a migration is shared (pushed, applied in another database).
  **`migrations remove`** (after `database update <PreviousMigration>`) is fine only for a migration that
  never left my machine.
- EF *may* detect a rename; when it can't, it generates drop + add, which loses the data. Read the file.

**`Import/ImportQueue.cs`:**
```csharp
public class ImportQueue
{
    private readonly Channel<long> _channel = Channel.CreateUnbounded<long>();

    public ValueTask EnqueueAsync(long runId, CancellationToken ct) =>
        _channel.Writer.WriteAsync(runId, ct);

    public IAsyncEnumerable<long> ReadAllAsync(CancellationToken ct) =>
        _channel.Reader.ReadAllAsync(ct);
}
```
`Channel<long>` is .NET's in-memory producer/consumer queue: the controller writes, the worker reads.

**`Import/ImportWorker.cs`:**
```csharp
public class ImportWorker(
    ImportQueue queue,
    IServiceScopeFactory scopeFactory,
    ILogger<ImportWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await MarkUnfinishedRunsAbandonedAsync(stoppingToken);

        await foreach (var runId in queue.ReadAllAsync(stoppingToken))
        {
            // The worker lives as long as the app, but a DbContext should be short-lived:
            // a fresh scope per run gives a fresh DbContext and importer.
            await using var scope = scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
            var importer = scope.ServiceProvider.GetRequiredService<CompanyImporter>();

            var run = await db.ImportRuns.FindAsync([runId], stoppingToken);
            if (run is null)
                continue;

            run.Status = ImportStatus.Running;
            run.StartedAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync(stoppingToken);
            logger.LogInformation("Import run {RunId} started", runId);

            ImportSummary? summary = null;
            string? error = null;
            try
            {
                summary = await importer.RunAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Import run {RunId} failed", runId);
                error = ex.Message;
            }

            // The importer clears the change tracker after every page, which also
            // forgets `run`. Attach it again before recording how the run ended.
            db.ChangeTracker.Clear();
            db.ImportRuns.Attach(run);

            run.Status = error is null ? ImportStatus.Succeeded : ImportStatus.Failed;
            run.Error = error;
            run.Read = summary?.Read ?? 0;
            run.Inserted = summary?.Inserted ?? 0;
            run.Updated = summary?.Updated ?? 0;
            run.Unchanged = summary?.Unchanged ?? 0;
            run.Rejected = summary?.Rejected ?? 0;
            run.FinishedAt = DateTimeOffset.UtcNow;
            await db.SaveChangesAsync(CancellationToken.None);

            logger.LogInformation("Import run {RunId} finished: {Status}", runId, run.Status);
        }
    }

    // The queue lives in memory, so a restart empties it. Runs that were Queued or Running
    // when the app stopped will never finish: mark them, so they don't block new imports.
    private async Task MarkUnfinishedRunsAbandonedAsync(CancellationToken ct)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var count = await db.ImportRuns
            .Where(r => r.Status == ImportStatus.Queued || r.Status == ImportStatus.Running)
            .ExecuteUpdateAsync(s => s
                .SetProperty(r => r.Status, ImportStatus.Abandoned)
                .SetProperty(r => r.FinishedAt, DateTimeOffset.UtcNow), ct);

        if (count > 0)
            logger.LogWarning("Marked {Count} unfinished import run(s) as Abandoned", count);
    }
}
```
- **`BackgroundService`:** started with the app; `ExecuteAsync` lives until shutdown, when
  `stoppingToken` fires.
- **`await foreach` over `ReadAllAsync`** is what sleeps until an id arrives. `if (run is null)
  continue;` only skips an id whose row is gone.
- **Why `IServiceScopeFactory`:** the worker is a singleton (created once); `AppDbContext` is scoped
  (one per request), and a singleton can't take a scoped service in its constructor. So it creates its
  own scope per run and asks that scope for the DbContext and importer.
- **`await using var scope` inside the loop:** `CreateAsyncScope()` gives *new* instances each pass;
  `await using` *disposes* that pass's scope (and its DbContext: connection back to the pool, tracker
  gone) when the pass ends. Overwriting a variable doesn't clean anything up in C#; a scope declared
  outside the loop and reassigned would leak a connection per run. `await using` = `try/finally` +
  `DisposeAsync()`.
- **`catch (...) when (ex is not OperationCanceledException)`:** an exception filter. A real error →
  `Failed`; shutting down isn't a failure.
- **`CancellationToken.None` on the last save:** record the result even if shutdown has started.

**Clear and Attach, the part that took a while:** the worker and the importer get **the same
`AppDbContext`** from the same scope, so they share **one change tracker**.
- The **importer's** `Clear()` (after every page, inside `RunAsync`) wipes the tracker, and with it the
  note about `run`. That's what forces `Attach`, whatever the worker does. Without it, setting
  `run.Status` and saving saves **nothing**, with no error: the run stays Running forever.
- `db.ImportRuns.Attach(run)` puts `run` back on **this DbContext's** tracker (Unchanged, snapshot =
  current values). Each DbContext has its own tracker; it must be the same `db` you save with. Attach
  **first**, then change; changed-then-attached values are in the snapshot, so nothing is saved.
- The **worker's** `Clear()` is for the crash case only: a page that failed half-way leaves its companies
  tracked as Added, and the final save would try to insert them too.
- **Alternative without Attach:** after `Clear()`, reload: `run = await db.ImportRuns.FindAsync([runId],
  CancellationToken.None);` (plus a null check). A loaded object is tracked automatically. One extra
  query, no tracker thinking.

**`ExecuteUpdateAsync`:** a bulk `UPDATE ... WHERE` sent straight to the database; returns the number of
rows; no loading, no tracker, no `SaveChangesAsync`. Use it for bulk changes; load-change-save when you
need the objects. Catch: objects already loaded in memory don't see the change.

**Tracking, in general:** queries that return entities (and `FindAsync`) are tracked unless
`AsNoTracking()`. A `Select` into a DTO isn't tracked (it isn't an entity). `ExecuteUpdate/Delete` never
track.

**`Contracts/ImportRunDto.cs`:**
```csharp
public record ImportRunDto(
    long Id, string Status, DateTimeOffset QueuedAt, DateTimeOffset? StartedAt, DateTimeOffset? FinishedAt,
    int Read, int Inserted, int Updated, int Unchanged, int Rejected, string? Error)
{
    public static ImportRunDto Fill(ImportRun r) => new(
        r.Id, r.Status.ToString(), r.QueuedAt, r.StartedAt, r.FinishedAt,
        r.Read, r.Inserted, r.Updated, r.Unchanged, r.Rejected, r.Error);
}
```
A static mapper method on the record: one place that turns the entity into the DTO.

**`Controllers/ImportsController.cs`:**
```csharp
[ApiController]
[Route("api/imports")]
public class ImportsController(AppDbContext db, ImportQueue queue) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Start(CancellationToken ct)
    {
        var active = await db.ImportRuns.AnyAsync(
            r => r.Status == ImportStatus.Queued || r.Status == ImportStatus.Running, ct);
        if (active)
            return Problem(statusCode: StatusCodes.Status409Conflict, title: "An import is already queued or running");

        var run = new ImportRun { Status = ImportStatus.Queued, QueuedAt = DateTimeOffset.UtcNow };
        db.ImportRuns.Add(run);
        await db.SaveChangesAsync(ct);
        await queue.EnqueueAsync(run.Id, ct);

        return Accepted($"/api/imports/{run.Id}", ImportRunDto.Fill(run));
    }

    [HttpGet("{id:long}")]
    public async Task<ActionResult<ImportRunDto>> Get(long id, CancellationToken ct)
    {
        var run = await db.ImportRuns.AsNoTracking().FirstOrDefaultAsync(r => r.Id == id, ct);
        return run is null ? NotFound() : ImportRunDto.Fill(run);
    }

    [HttpGet]
    public async Task<List<ImportRunDto>> List(CancellationToken ct)
    {
        var runs = await db.ImportRuns.AsNoTracking().OrderByDescending(r => r.Id).Take(20).ToListAsync(ct);
        return runs.Select(ImportRunDto.Fill).ToList();
    }
}
```
- **Save, then enqueue:** `run.Id` is `0` until `SaveChangesAsync`, which sends `INSERT ... RETURNING id`
  and writes the new id back into the object.
- **`IActionResult`:** `Start` returns either a 409 problem or a 202 with the run, two different shapes.
  It doesn't return an `ImportSummary`: that doesn't exist yet; the worker makes it later.
- **`Accepted(url, body)`:** 202 + a `Location: /api/imports/5` header + the run. Returning the DTO
  directly would give 200. (`AcceptedAtAction(nameof(Get), new { id = run.Id }, ...)` builds the same
  URL from `Get`'s route instead of a string; it doesn't call `Get`.)
- `new { id = run.Id }` is an **anonymous type**: an object without a class, like `['id' => $id]`.
- `{id:long}` is a route constraint: `/api/imports/abc` gives 404.

**`Program.cs`:**
```csharp
builder.Services.AddSingleton<ImportQueue>();      // ONE queue shared by the controller and the worker
builder.Services.AddHostedService<ImportWorker>();
```
As scoped, every request would get its own empty queue, and the worker would never see the ids.

```bash
curl -si -X POST localhost:5020/api/imports | grep -E "^HTTP|^Location"   # 202 + Location
curl -si -X POST localhost:5020/api/imports | head -1                     # 409 while it runs
sleep 10; curl -sS localhost:5020/api/imports | jq                        # Succeeded, with counts
# Abandoned: POST, Ctrl+C within 2 s, start again → "Marked 1 unfinished import run(s) as Abandoned"
```

---

## Mistakes and what they taught

| Step | Mistake | Lesson |
|---|---|---|
| 2 | Injected `AppDbContext` into the `Company` entity | The entity is just data; the DbContext holds the `DbSet`, the controller gets the DbContext |
| 2 | `public string Name;` (a field) | EF maps properties: `{ get; set; }` |
| 2 | A stray `using Microsoft.AspNetCore.Components;` | The editor adds usings on autocomplete; remove the ones you didn't mean |
| 4 | `CountAsync()` without `ct` | Pass the `CancellationToken` to *every* async EF call, or that query can't be cancelled |
| 4 | `OrgForm` compared as typed | `?orgForm=as` found 0 rows: normalise input (`Trim().ToUpperInvariant()`) before comparing |
| 4 | `IReadOnlyList<T> items` (lower case) | Record parameters become public properties: PascalCase (`Items`). The JSON is camelCase either way |
| 6 | `OrganisasjonsForm` (capital F) | It still worked (case-insensitive), but our own JSON then said `organisasjonsForm`. Copy the outside API's spelling: `Organisasjonsform` |
| 7 | Moved the files but kept `using Practice2.Api.Brreg;` in Program.cs | A `using` names a namespace; when the namespace changes, every `using` of it must change too |
| 7 | `curl` gave 404 after adding the controller | The running API was the old build. Restart after every change (or `dotnet watch`) |
| 7 | Thought `Add(brregItem)` could insert directly | `Add` needs our entity, not Brreg's record: map `BrregEnhet` → `Company` first |
| 8 | `FinishedT` typo and no `?` on the start/finish times | Read the migration's SQL before `database update`. Fixed with two migrations on top |
| 8 | "dotnet watch won't work" | The app crashed on startup: a throwing `BackgroundService` stops the host. Read the log for the first `fail:` |
| 8 | `FinishededAt` and no `?` in the DTO too | The DTO mirrors the entity's nullability; fix both |
