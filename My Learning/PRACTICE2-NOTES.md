# Practice2 notes: the API, step by step

The second rebuild of the API, in `practice2/`. One section per step: the commands, the code, and why
each part is there. The table below is the step list, including the steps still to come; the React steps
and the later rounds are in [What comes next](#what-comes-next). The concepts are in
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
| 9a | `Role` entity and the `roles` table: a navigation, a natural key, a check constraint | ✅ |
| 9b | Roles import: one Brreg call per company, 5 at a time, replace each chunk's roles in a transaction | ✅ |
| 9c | `GET /api/companies/{orgnr}/roles`: `Include`, a code → name dictionary, 400/404/200 `[]` | ✅ |
| 9d | `GET /api/roles` search: person-or-unit name, comma-separated lists, birth-year range, the company via the navigation | ✅ |
| 10 | `users` table: unique username, PBKDF2 password hash, space-separated scopes; a hash tool | ✅ |
| 11a | `JwtOptions`: typed settings, the signing key in user-secrets, validated at startup | ✅ |
| 11b | `TokenService`: claims, expiry, HMAC signature; what's inside a JWT | ✅ |
| 11c | `POST /api/auth/login`: verify the hash, the same 401 for an unknown user and a wrong password | ✅ |
| 11d | JWT validation, scope policies on the endpoints, 401/403 | ✅ |
| 11e | A fallback policy: every endpoint needs a login unless it's marked open | ⏳ |
| 12 | Correlation id middleware and structured logs | ⏳ |
| 13 | Integration tests, also a login test and a roles test | ⏳ |

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

## Step 9a: the `roles` table

**Why:** a company's roles from Brreg (`/enheter/{orgnr}/roller`): daglig leder, styreleder, members,
the auditor. One row per role. The holder is **either a person** (name + birth year) **or another unit**
(an orgnr + name, e.g. the auditing firm). Filled by the importer in 9b.

**Decisions:**
- **Only the code** (`LEDE`, `DAGL`) is stored; the names come from a dictionary in code (9c). Not a C#
  enum: Brreg can add a code, and an enum can't hold a value it doesn't know.
- **The birth year only**, not the full `fodselsdato`: GDPR data minimisation; the app doesn't need more.
- **`UnitName` is stored**, not looked up: the auditor is usually not in our `companies` table, so a
  foreign key to it isn't possible.
- **No bostyrer** (an estate administrator: a name, no birth date, no orgnr). 9b skips them.
- **No start/end dates:** Brreg doesn't give them. History is a practice4 idea.

> **Changed in 9b:** we decided to **mirror Brreg** as it publishes: store every role, skip nothing.
> A bostyrer's name goes in `PersonName`. The unique index `ux_roles_identity` was dropped (the
> importer replaces a company's roles, so a re-run can't repeat them), and EF then added
> `ix_roles_orgnr` for the foreign key by itself. The sections below on the natural key and
> `NULLS NOT DISTINCT` stay: the idea is worth knowing.

**`Data/Role.cs`:**
```csharp
public class Role
{
    public long Id { get; set; }

    public required string Orgnr { get; set; }
    public Company? Company { get; set; }

    public required string RoleType { get; set; }

    // The holder is either a person or another unit, never both.
    public string? PersonName { get; set; }
    public int? BirthYear { get; set; }
    public string? UnitOrgnr { get; set; }
    public string? UnitName { get; set; }

    public bool Resigned { get; set; }
}
```
In `Company.cs`, the other end of the link: `public List<Role>? Roles { get; set; }`.

In `AppDbContext`: `public DbSet<Role> Roles => Set<Role>();` and
```csharp
modelBuilder.Entity<Role>(role =>
{
    role.Property(r => r.Orgnr).HasMaxLength(9).IsFixedLength();
    role.Property(r => r.RoleType).HasMaxLength(10);
    role.Property(r => r.PersonName).HasMaxLength(300);
    role.Property(r => r.UnitOrgnr).HasMaxLength(9).IsFixedLength();
    role.Property(r => r.UnitName).HasMaxLength(500);

    role.HasOne(r => r.Company)
        .WithMany(c => c.Roles)
        .HasForeignKey(r => r.Orgnr)
        .OnDelete(DeleteBehavior.Cascade);

    // What identifies a role: the company, the type and the holder. NULLs count as equal,
    // so two rows with the same person and no unit are duplicates too.
    role.HasIndex(r => new { r.Orgnr, r.RoleType, r.PersonName, r.BirthYear, r.UnitOrgnr })
        .IsUnique()
        .AreNullsDistinct(false)
        .HasDatabaseName("ux_roles_identity");

    role.HasIndex(r => r.RoleType);
    role.HasIndex(r => r.BirthYear);

    role.ToTable(t => t.HasCheckConstraint(
        "ck_roles_one_holder", "(person_name IS NULL) <> (unit_orgnr IS NULL)"));
});
```

### The navigation property `Company? Company`

Two properties describe the same link:

| Property | What it is | In the table? |
|---|---|---|
| `Orgnr` | the **foreign key**: the value `"811577472"` | yes, the column `orgnr` |
| `Company` | the **navigation**: the whole `Company` object that orgnr points to | no, never a column |

The foreign key is enough for the relationship and the `FOREIGN KEY` constraint. The navigation is a
convenience, so C# can "walk" from a role to its company, and EF writes the JOIN:
```csharp
db.Roles.Select(r => new { r.RoleType, r.PersonName, CompanyName = r.Company!.Name })
// SELECT r.role_type, r.person_name, c.name FROM roles r INNER JOIN companies c ON r.orgnr = c.orgnr
```
Like Laravel: `orgnr` is the column, `$role->company` is the `belongsTo` relation.
- It is **`null` unless loaded**: `db.Roles.First()` gives `Company == null` (EF doesn't load it by itself).
  Inside a `Select` it's just a JOIN, or `.Include(r => r.Company)` loads the whole object.
- **`?`** because of that, and **`!`** in `r.Company!.Name` tells the compiler "I know it's there" (in SQL
  it always is).

### `HasOne(...).WithMany()`

The relationship has two ends, so the call has two halves:
- `HasOne(r => r.Company)`: **this** side. A role has one company, reached by `r.Company`.
- `WithMany(c => c.Roles)`: the **other** side. A company has many roles, reached by `company.Roles`
  (Laravel's `hasMany`). `WithMany()` with empty brackets would mean `Company` has no such list. The
  list is a navigation too: no column, no migration (`has-pending-model-changes` said "No changes"),
  and `null` unless loaded (`.Include(c => c.Roles)`).
- `HasForeignKey(r => r.Orgnr)`: the column that holds the link. EF's convention would look for a
  property named `CompanyOrgnr`; ours is named `Orgnr`, and this line tells EF so. No `CompanyOrgnr`
  is needed.
- `OnDelete(Cascade)`: delete a company → its roles go too (`ON DELETE CASCADE`).

### Why not a primary key of the 4 fields

**A primary key column can never be NULL**, and every role has `person_name` *or* `unit_orgnr` NULL.
So: `id` is the primary key, and the natural key is a **unique index**. It gives the same guarantee:
"these fields together are unique". `birth_year` is in it, so two different "Ole Hansen"s stay apart.
In practice3 (a `persons` table, so every role has a non-null `PersonId`), a composite key works:
`HasKey(r => new { r.Orgnr, r.RoleType, r.PersonId })`.

### `AreNullsDistinct(false)` = `NULLS NOT DISTINCT`

**In one sentence:** compare NULL like a normal value, so `(Kari, NULL)` and `(Kari, NULL)` count as the same key.

In SQL, `NULL = NULL` is not true but *unknown*. So by default a unique index treats **every NULL as
different from every other NULL**, and lets "duplicates" in as long as one column is NULL:

| orgnr | role_type | person_name | unit_orgnr | default (NULLS DISTINCT) | NULLS NOT DISTINCT |
|---|---|---|---|---|---|
| 123456789 | DAGL | Kari Nordmann | NULL | inserted | inserted |
| 123456789 | DAGL | Kari Nordmann | NULL | **inserted too** (NULL ≠ NULL) | **ERROR: duplicate key** |
| 123456789 | MEDL | Kari Nordmann | NULL | inserted | inserted (another type) |

Our rows *always* have a NULL (no person or no unit), so without `NULLS NOT DISTINCT` the index would
never block anything. (PostgreSQL 15+.) Tried on a temp table:
```
ERROR:  duplicate key value violates unique constraint "..."
DETAIL:  Key (orgnr, role_type, person_name, unit_orgnr)=(123456789, DAGL, Kari Nordmann, null) already exists.
```

### Which columns get an index

An index costs space and a little time on every insert, so it's added for a reason: a column that
`WHERE`, `JOIN` or `ORDER BY` will use often.
- `orgnr`: "roles of this company" (9c) and the JOIN. **Covered by `ux_roles_identity`**, because it is
  the first column (an index on `(a, b, c)` also serves `WHERE a = ...`, like a phone book sorted by last
  name, then first name). So EF didn't add its own index for the foreign key. (After 9b dropped the
  unique index, EF added `ix_roles_orgnr` by itself.)
- `role_type`, `birth_year`: the 9d filters (`?roleType=DAGL`, `?birthYear=1970`). In the big index they
  are 2nd and 4th, so a filter on them alone can't use it.
- `person_name`: the 9d name search is `ILIKE '%hansen%'`; a normal index doesn't help a leading `%`.
- `unit_orgnr`, `unit_name`, `resigned`: nothing filters on them yet.

### `ck_roles_one_holder`

`(person_name IS NULL) <> (unit_orgnr IS NULL)`: each side is true/false, and `<>` means "different",
so exactly one of them is NULL (an XOR):

| person_name | unit_orgnr | `IS NULL` / `IS NULL` | Allowed |
|---|---|---|---|
| 'Kari Nordmann' | NULL | false / true | ✅ a person |
| NULL | '987654321' | true / false | ✅ a unit |
| 'Kari Nordmann' | '987654321' | false / false | ❌ both |
| NULL | NULL | true / true | ❌ none |

It checks only those two columns: `birth_year` and `unit_name` aren't part of the rule.

### The two sequences

`\d` lists `import_runs_id_seq` and `roles_id_seq`: one per `GENERATED ... AS IDENTITY` column. A
sequence is a counter object that hands out the next id (`nextval`); `companies` has none because its key
is the orgnr. **A sequence never rolls back:** a failed or rolled-back insert still used its number, so
ids can have gaps. (My two test inserts above failed, so the first real role gets id 3.) Like MySQL's
`AUTO_INCREMENT`, but a separate object.

```bash
dotnet ef migrations add AddRoles -o Data/Migrations
dotnet ef migrations script MakeImportRunTimesNullable     # READ the SQL
dotnet ef database update
```
Then `\d roles`: the columns, `pk_roles`, three indexes (`ux_roles_identity ... NULLS NOT DISTINCT`),
`ck_roles_one_holder`, and `fk_roles_companies_orgnr ... ON DELETE CASCADE`.

## Step 9b: the roles import

**Why:** fill `roles`. Brreg gives roles **one company at a time**
(`GET /enhetsregisteret/api/enheter/{orgnr}/roller`), so the import makes one call per company: about
7,200 calls. It runs in the same import run, right after the companies.

**Decision: mirror Brreg.** Store every role as Brreg publishes it: no skipping, no unique index. Roles
are a **snapshot** (they change all together, in content and in number), so each company's roles are
**replaced**, not upserted. Any cleaning or pre-calculation belongs elsewhere (controllers, the
frontend, or tables filled at night), which is common in real systems.
(For the whole of Norway there is a bulk download, `/roller/totalbestand`; too much for two municipalities.)

### Brreg's JSON, and why the model needs every level

```json
{ "rollegrupper": [
    { "type": { "kode": "STYR" },
      "roller": [
        { "type": { "kode": "LEDE" }, "avregistrert": false,
          "person": { "navn": { "fornavn": "Kari", "etternavn": "Nordmann" }, "fodselsdato": "1970-04-04" } },
        { "type": { "kode": "REVI" },
          "enhet": { "organisasjonsnummer": "922633452", "navn": ["AIDER AS"] } },
        { "type": { "kode": "BOBE" },
          "bostyrer": { "navn": "Adv. Ola Hansen" } } ] } ] }
```
The holder is one of three: `person`, `enhet` (another unit; its `navn` is a **list**), or `bostyrer`
(a bankruptcy estate's administrator, only a name).

> **Essential when reading JSON:** `ReadFromJsonAsync<T>` must be given a type that matches the **top**
> of the JSON. Here the top is `{ "rollegrupper": [...] }`, so `T` is `BrregRoles`, which holds
> `BrregRoleGroup`s, which hold `BrregRole`s. You can't skip a level and ask for `List<BrregRole>`
> directly: the reader would look for a list at the top and find an object. So the model has a record
> for **every level on the way down**, but only with the fields you need (the rest are ignored). Then
> the code flattens it.

**`Import/BrregModel.cs`** (added):
```csharp
// GET /enhetsregisteret/api/enheter/{orgnr}/roller
public record BrregRoles(IReadOnlyList<BrregRoleGroup>? Rollegrupper);

public record BrregRoleGroup(IReadOnlyList<BrregRole>? Roller);

public record BrregRole(BrregRoleType? Type, BrregPerson? Person, BrregRoleUnit? Enhet, BrregBostyrer? Bostyrer, bool Avregistrert);

public record BrregRoleType(string? Kode);

public record BrregPerson(BrregPersonName? Navn, DateOnly? Fodselsdato);

public record BrregPersonName(string? Fornavn, string? Mellomnavn, string? Etternavn);

public record BrregRoleUnit(string? Organisasjonsnummer, IReadOnlyList<string>? Navn);

public record BrregBostyrer(string? Navn);
```
`BrregBostyrer` is only the incoming shape: the entity gets no new field, its name goes in
`PersonName`. Without it the reader would drop the `bostyrer` field, and the role would have no holder.

**Another option: `jsonb`.** Store each company's raw JSON in one `jsonb` column (a "landing table"),
and pick fields later with SQL (`data->>'navn'`). The model stays tiny, which helps when the JSON is
big or changes often. The cost: no types or constraints, and filters need special indexes. Common as a
first stage ("land raw, transform later"). Here we filter on the fields, so columns are right.

### `BrregClient.GetRolesAsync`: the client hides the nesting
```csharp
// Brreg nests the roles in groups; the importer only needs the roles themselves.
public async Task<IReadOnlyList<BrregRole>> GetRolesAsync(string orgnr, CancellationToken ct)
{
    var response = await http.GetAsync($"enhetsregisteret/api/enheter/{orgnr}/roller", ct);

    // A company deleted from Brreg since the company import: no roles, not an error.
    if (response.StatusCode == HttpStatusCode.NotFound)
        return [];

    response.EnsureSuccessStatusCode();
    var result = await response.Content.ReadFromJsonAsync<BrregRoles>(ct);
    return result?.Rollegrupper?.SelectMany(g => g.Roller ?? []).ToList() ?? [];
}
```
- `GetAsync` instead of `GetFromJsonAsync`: we can look at the status first. `GetFromJsonAsync` throws
  on 404, and one deleted company would fail the whole run. Other errors still throw
  (`EnsureSuccessStatusCode`), after the resilience handler's retries.
- `SelectMany` flattens the groups' role lists into one list, like
  `array_merge(...array_column($groups, 'roller'))`.
- The importer only sees `BrregRole`: `BrregRoles` and `BrregRoleGroup` stay inside the client.

### `Import/RoleImporter.cs`
```csharp
public record RoleImportSummary(int Companies, int Saved);

public class RoleImporter(AppDbContext db, BrregClient brreg, ILogger<RoleImporter> logger)
{
    // Also how many Brreg calls run at the same time.
    private const int ChunkSize = 5;

    public async Task<RoleImportSummary> RunAsync(CancellationToken ct)
    {
        var allOrgnrs = await db.Companies.OrderBy(c => c.Orgnr).Select(c => c.Orgnr).ToListAsync(ct);
        int done = 0, saved = 0;

        foreach (var chunk in allOrgnrs.Chunk(ChunkSize))
        {
            // 1. Start one call per company (no await: each call starts and runs on),
            //    keyed by orgnr, then wait until all of them have answered.
            var calls = new Dictionary<string, Task<IReadOnlyList<BrregRole>>>();
            foreach (var orgnr in chunk)
                calls[orgnr] = brreg.GetRolesAsync(orgnr, ct);
            await Task.WhenAll(calls.Values);

            // 2. Map every role, as Brreg sends it: for each company, for each of its roles.
            var newRoles = new List<Role>();
            foreach (var (orgnr, call) in calls)
            {
                var brregRoles = await call;   // already finished: returns at once
                foreach (var brregRole in brregRoles)
                    newRoles.Add(FillRole(orgnr, brregRole));
            }

            // 3. Replace the chunk's roles: delete the old, insert the new, all or nothing.
            await using var transaction = await db.Database.BeginTransactionAsync(ct);
            await db.Roles.Where(r => chunk.Contains(r.Orgnr)).ExecuteDeleteAsync(ct);
            db.Roles.AddRange(newRoles);
            await db.SaveChangesAsync(ct);
            await transaction.CommitAsync(ct);
            db.ChangeTracker.Clear();

            done += chunk.Length;
            saved += newRoles.Count;
            if (done % 500 == 0 || done == allOrgnrs.Count)
                logger.LogInformation("Roles: {Done} of {Total} companies", done, allOrgnrs.Count);
        }

        return new RoleImportSummary(done, saved);
    }

    // The holder is a person, another unit, or a bostyrer (a bankruptcy estate's
    // administrator: only a name, stored as the person's name).
    private static Role FillRole(string orgnr, BrregRole brregRole)
    {
        var role = new Role
        {
            Orgnr = orgnr,
            RoleType = brregRole.Type!.Kode!,
            Resigned = brregRole.Avregistrert
        };

        if (brregRole.Enhet is not null)
        {
            role.UnitOrgnr = brregRole.Enhet.Organisasjonsnummer;
            role.UnitName = string.Join(" ", brregRole.Enhet.Navn ?? []);
        }
        else if (brregRole.Person?.Navn is not null)
        {
            var navn = brregRole.Person.Navn;
            role.PersonName = string.Join(" ",
                new[] { navn.Fornavn, navn.Mellomnavn, navn.Etternavn }.Where(p => !string.IsNullOrWhiteSpace(p)));
            role.BirthYear = brregRole.Person.Fodselsdato?.Year;
        }
        else if (brregRole.Bostyrer is not null)
        {
            role.PersonName = brregRole.Bostyrer.Navn;
        }

        return role;
    }
}
```
**In one breath (for an interview):** "Load all orgnrs. In chunks of 5: start the 5 Brreg calls at once
and wait for all of them. Map every role to our entity. Then delete the chunk's old roles and insert the
new ones in one transaction, because roles are a snapshot. Clear the tracker, log, return the counts."

- **`Chunk(5)`** splits the list into arrays of 5 (PHP's `array_chunk`). Here it is also the limit on
  parallel calls: polite to a free public API.
- **Calling an async method without `await` starts it** and gives back a `Task` (a promise). `await`
  waits. So `await` inside the first `foreach` would mean one call at a time.
- **`Task.WhenAll`** = `Promise.all`: wait until all have finished.
- **`Dictionary<string, Task<IReadOnlyList<BrregRole>>>`**: a promise per orgnr; each promise gives that
  company's roles (the same type `GetRolesAsync` returns). Keyed by orgnr because **a role in Brreg's
  JSON doesn't say which company it belongs to**; that's only in the URL.
- `foreach (var (orgnr, call) in calls)` = `foreach ($calls as $orgnr => $call)`. `await call` on a
  finished task returns at once (`call.Result` works too, but `.Result` is a habit interviewers flag).
- **Two loops**, because the data has two levels: companies, then each company's roles.
- **The transaction:** `ExecuteDeleteAsync` sends `DELETE ... WHERE orgnr = ANY(...)` at once, without
  loading rows (like step 8's `ExecuteUpdateAsync`). Without a transaction, a crash after the delete
  would leave those companies with no roles. With it, the delete and the inserts commit together, and
  `await using` rolls back if anything throws before `CommitAsync`.
- **`Type!.Kode!`**: Brreg always sends a code; if not, the `NOT NULL` column fails loudly.
- **The DbContext is only used after `WhenAll`**, one step at a time. The HTTP calls run in parallel
  (HttpClient is thread-safe); a DbContext must never be used by two threads at once.
- **Why it takes ~66 s, not 18 min:** `curl` took 0.15 s per call because each `curl` opens a new
  connection (TLS handshake). The app's HttpClient keeps connections open (step 6), about 0.04 s per
  call: ~4.5 min one at a time, ~66 s with 5 at once.

**The rest:**
- `Data/ImportRun.cs`: `public int RolesSaved { get; set; }`, and the DTO gets `int RolesSaved`.
- `Program.cs`: `builder.Services.AddScoped<RoleImporter>();`
- `ImportWorker.cs`: get a `RoleImporter` from the same scope; in the `try`, after the companies:
  `roleSummary = await roleImporter.RunAsync(stoppingToken);`; at the end:
  `run.RolesSaved = roleSummary?.Saved ?? 0;`. Both importers share one DbContext; fine, since they
  run one after the other.
- `AppDbContext`: the `ux_roles_identity` block removed.

```bash
dotnet ef migrations add RoleImportChanges -o Data/Migrations
dotnet ef migrations script AddRoles     # DROP INDEX ux_roles_identity; ADD roles_saved; CREATE INDEX ix_roles_orgnr
dotnet ef database update
```
**Result (run 13):** 72 s, 7,198 companies read, **26,472 roles** (19,443 persons, 7,029 units,
55 bostyrer). Codes: LEDE, MEDL, DAGL, REGN, REVI, VARA, BOBE, FFØR. `FFØR` (forretningsfører) wasn't
in the first sample: why role names are a dictionary, not an enum.

### When migrations go wrong: `remove`, never delete by hand

The **snapshot** (`AppDbContextModelSnapshot.cs`) is EF's picture of the model after all migrations.
`migrations add` compares the model with the snapshot, writes the difference, and updates the snapshot.
- I ran `migrations add` with `-o Date/Migrations` (typo), then deleted that folder by hand. The
  snapshot kept the new columns, so the next `add` found **no difference** and wrote an **empty**
  migration. `database update` applied it: "applied", but no columns.
- **`dotnet ef migrations remove`** deletes the migration files **and reverts the snapshot**. For a
  migration already applied: `database update <PreviousMigration>` first, then `remove`. Only for
  migrations that were never pushed; otherwise fix on top.
- `dotnet ef` builds the project first: one compile error blocks every `ef` command.

## Step 9c: `GET /api/companies/{orgnr}/roles`

**Why:** a company's roles, each with a readable name ("Styrets leder", not only `LEDE`). The first real
use of the navigation `Company.Roles`.

### `Domain/RoleTypes.cs`: role names, keyed by code
```csharp
// Brreg's role codes and their names (from /enhetsregisteret/api/roller/rolletyper).
// Only the codes are stored; a code missing here is shown as itself.
public static class RoleTypes
{
    private static readonly Dictionary<string, string> Names = new()
    {
        ["DAGL"] = "Daglig leder",
        ["LEDE"] = "Styrets leder",
        ["NEST"] = "Nestleder",
        ["MEDL"] = "Styremedlem",
        ["VARA"] = "Varamedlem",
        ["OBS"] = "Observatør",
        ["REGN"] = "Regnskapsfører",
        ["REVI"] = "Revisor",
        ["FFØR"] = "Forretningsfører",
        ["BOBE"] = "Bostyrer",
        ["KONT"] = "Kontaktperson",
        ["KDEB"] = "Konkursdebitor",
    };

    public static string TextOf(string code) => Names.GetValueOrDefault(code, code);
}
```
- `new() { ["DAGL"] = "..." }` is a dictionary initializer: `['DAGL' => '...']`.
- **A dictionary, not an enum:** an unknown code (Brreg has 27) falls back to the code itself.
- **`Names[code] ?? code` does NOT work in C#.** The indexer **throws `KeyNotFoundException`** for a
  missing key (PHP returns null, C# doesn't), so `??` is never reached and the endpoint gives 500.
  `GetValueOrDefault(key, fallback)` is the C# version of PHP's `$names[$code] ?? $code`.
- The import never uses names (it stores codes), so the fallback protects the **endpoint**.

### `Contracts/RoleDto.cs`
```csharp
public record RoleDto(
    string RoleType,
    string RoleName,
    string? PersonName,
    int? BirthYear,
    string? UnitOrgnr,
    string? UnitName,
    bool Resigned);
```
No `Id`, `Orgnr` or `Company`: the caller knows the company, and our internal id isn't the API's business.

### The action, in `CompaniesController`
```csharp
[HttpGet("{orgnr}/roles")]
public async Task<ActionResult<List<RoleDto>>> GetRoles(string orgnr, CancellationToken ct)
{
    if (!Orgnr.IsValid(orgnr))
    {
        return Problem(
            statusCode: StatusCodes.Status400BadRequest,
            title: "Invalid ORGNR",
            detail: "An ORGNR should be 9 digits with a valid MOD11 check digit."
        );
    }

    // Include: load the company AND its roles, in one query (a LEFT JOIN).
    var company = await db.Companies.AsNoTracking()
        .Include(c => c.Roles)
        .FirstOrDefaultAsync(c => c.Orgnr == orgnr, ct);
    if (company is null)
        return NotFound();

    return company.Roles!
        .Select(r => new RoleDto(
            r.RoleType, RoleTypes.TextOf(r.RoleType),
            r.PersonName, r.BirthYear, r.UnitOrgnr, r.UnitName, r.Resigned))
        .OrderBy(r => r.RoleName)
        .ThenBy(r => r.PersonName ?? r.UnitName)
        .ToList();
}
```
- **`Include(c => c.Roles)`** = Laravel's `Company::with('roles')->find($orgnr)`. EF sends one query:
  `SELECT ... FROM (SELECT ... FROM companies WHERE orgnr = @orgnr LIMIT 1) c0 LEFT JOIN roles r ON c0.orgnr = r.orgnr`
  and fills `company.Roles`. Without `Include`, `Roles` stays `null`.
- **The LEFT JOIN keeps the three cases apart:**

| Request | Result |
|---|---|
| `/api/companies/123456789/roles` (bad MOD11) | **400** |
| a valid orgnr not in our table | **404** |
| a company with no roles (18 of 7,228) | **200** `[]` |

- **Why `OrderBy` comes after `Select` here.** "Where/OrderBy first, Select last" is for an `IQueryable`
  (SQL not sent yet): the order shapes the SQL. After `FirstOrDefaultAsync` the query has run, and
  `company.Roles` is a plain list in memory: ordinary LINQ, like `array_map` + `usort`. And `RoleName`
  doesn't exist in the database; it's made in the `Select`, so it can only be sorted after.
  **Rule of thumb:** before the `await ...Async(ct)` you're building SQL; after it, you're working on a list.

### Ordering by "power" (added after 9c's commit; committed with 9d)
Keep the name and a rank together, keyed by code, using a **tuple** (two named values, no class).
`NameOf` / `RankOf` replaced `TextOf`. Every value must be a full pair: `("Bostyrer")` alone is just a
string in brackets, a compile error. And `GetValueOrDefault(code, code)` no longer fits: the fallback must
have the value's type, `(string, int)`, so each method picks its own part:
```csharp
private static readonly Dictionary<string, (string Name, int Rank)> Types = new()
{
    ["DAGL"] = ("Daglig leder", 10),
    ["LEDE"] = ("Styrets leder", 20),
    // ...
};
public static string NameOf(string code) => Types.ContainsKey(code) ? Types[code].Name : code;
public static int RankOf(string code) => Types.TryGetValue(code, out var type) ? type.Rank : 999;  // unknown: last
```
```csharp
company.Roles!
    .OrderBy(r => RoleTypes.RankOf(r.RoleType))
    .ThenBy(r => r.BirthYear ?? int.MaxValue)   // oldest first; units (no birth year) last
    .Select(r => new RoleDto(...))
```
`ContainsKey` + `Types[code]` is PHP's `isset($t[$code]) ? $t[$code] : ...`: safe, the key was just
checked; it looks up twice, `TryGetValue` once (no real difference for 12 codes).

**Sort before or after the `Select`?** In memory, either. Each LINQ step works on what the step before
produced: after the `Select` the items are `RoleDto`s, which still have `RoleType`, so
`.OrderBy(d => RoleTypes.RankOf(d.RoleType))` works too (that's what the code does). The rank is
*calculated* from the code; it doesn't need to be a field. What you can't do after the `Select` is sort
by something the DTO no longer has (`r.Id`). Sorting on the entity first works whatever the DTO holds.
(In SQL, too, ORDER BY may use columns not selected; the exception is `SELECT DISTINCT`.)

Sorted here on the DTO, and the rank never reaches the JSON. In memory, C# can
call `RankOf`. In a **database** query it can't ("could not be translated"): write it inline
(`r.RoleType == "DAGL" ? 10 : ...` becomes SQL `CASE WHEN`) or store the rank in a `role_types` table.

### A company *with* its relationships: one DTO per response shape
```csharp
public record CompanyDetailsDto(
    string Orgnr, string Name, string OrgForm, DateOnly? RegistrationDate,
    List<RoleDto> Roles,
    List<ShareholderDto> Shareholders);
```
- Small DTOs (`RoleDto`, `ShareholderDto`) are building blocks, reused inside bigger ones. Each
  endpoint has one exact contract.
- **Not one big DTO with nullable relationships:** `roles: null` would mean either "not loaded here" or
  "has none", and every field becomes optional in the frontend. (Laravel's `whenLoaded()` does that;
  typed C# records prefer separate DTOs.)
- **`Include` + `Select` without the roles in the DTO:** EF **ignores the `Include`**. A `Select`
  decides what comes back; if the DTO has no roles, no roles are loaded.
- **Two ways to fill it:**
  1. Load the entity with `Include`, then map in C# with `Fill` methods (like `ImportRunDto.Fill`).
     Natural, and fine for one company.
  2. A nested `Select` inside the query: only the needed columns, no entities. Tested: works, one
     LEFT JOIN, and `RoleTypes.TextOf` can be called in it (EF runs it in C# for each row).
- With **two** collections (roles + shareholders) in one JOIN, rows multiply (7 × 20 = 140 for one
  company): the "Cartesian explosion". `AsSplitQuery()` sends one query per collection instead.
- Often simpler: keep `/companies/{orgnr}` lean, and let the frontend fetch `/roles` and
  `/shareholders` in parallel, only when shown.
- **`Include` then `Fill`, the natural way** (`RoleDto.Fill` and `CompanyDetailsDto.Fill` are in
  `Contracts/RoleDto.cs`, not used by an endpoint yet):
  ```csharp
  public static CompanyDetailsDto Fill(Company c) => new(
      c.Orgnr, c.Name, c.OrgForm, c.RegistrationDate,
      c.Roles!.Select(RoleDto.Fill).ToList());        // = Select(r => RoleDto.Fill(r))
  ```
  ```csharp
  var company = await db.Companies.AsNoTracking().Include(c => c.Roles)
      .FirstOrDefaultAsync(c => c.Orgnr == orgnr, ct);
  return company is null ? NotFound() : CompanyDetailsDto.Fill(company);
  ```
  Call `Fill` **after** the `await`, on the loaded entity. Inside an EF `Select`, EF can't see into the
  method, so it wouldn't know to fetch the roles.

```bash
curl -s localhost:5020/api/companies/810733772/roles | jq '.[].roleName'
curl -si localhost:5020/api/companies/123456789/roles | head -1    # 400
curl -si localhost:5020/api/companies/914778271/roles | head -1    # 404
curl -s  localhost:5020/api/companies/991865500/roles              # []
```

## Step 9d: `GET /api/roles`, the role search

**Why:** search all 26k roles: "every company where a Hansen has a role", "every daglig leder born
1970–1975", "everything Enter Revisjon AS audits". Step 4's `IQueryable` pattern, plus the company
through the navigation `Role.Company`. I wrote most of the controller myself.

**`Contracts/RoleSearchQuery.cs`:**
```csharp
public record RolesSearchQuery
{
    public string? Name { get; set; }
    public string? RoleType { get; set; }      // comma-separated: DAGL,LEDE
    public string? Orgnr { get; set; }         // comma-separated

    [Range(1900,2100)]
    public int? BirthYearFrom { get; set; }

    [Range(1900,2100)]
    public int? BirthYearTo { get; set; }

    [Range(1, 1000)]
    public int Page { get; set; } = 1;

    [Range(1, 100)]
    public int PageSize { get; set; } = 20;
}
```
`int?` = "not given". A plain `int` would arrive as `0`, and `>= 0` would match everything.

**`Contracts/RoleSearchItemDto.cs`:** like `RoleDto`, plus `Orgnr` and `CompanyName`: in a search across
companies, every row must say whose role it is.
```csharp
public record RoleSearchItemDto(
    string Orgnr, string CompanyName, string RoleType, string RoleName,
    string? PersonName, int? BirthYear, string? UnitOrgnr, string? UnitName, bool Resigned);
```

**`Controllers/RolesController.cs`:**
```csharp
[ApiController]
[Route("api/roles")]
public class RolesController(AppDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<PageResult<RoleSearchItemDto>> Search([FromQuery] RolesSearchQuery query, CancellationToken ct)
    {
        IQueryable<Role> roles = db.Roles.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(query.Name))
        {
            // A person or a unit: one of the two names is NULL, and NULL ILIKE ... is never true.
            var keyword = $"%{query.Name.Trim()}%";
            roles = roles.Where(r => EF.Functions.ILike(r.PersonName!, keyword)
                                  || EF.Functions.ILike(r.UnitName!, keyword));
        }
        // Comma-separated: ?roleType=DAGL,LEDE  →  WHERE role_type = ANY(@roleTypes)
        var roleTypes = SplitList(query.RoleType).Select(t => t.ToUpperInvariant()).ToList();
        if (roleTypes.Count > 0)
        {
            roles = roles.Where(r => roleTypes.Contains(r.RoleType));
        }
        var orgnrs = SplitList(query.Orgnr);
        if (orgnrs.Count > 0)
        {
            roles = roles.Where(r => orgnrs.Contains(r.Orgnr));
        }

        if (query.BirthYearFrom is not null)
        {
            roles = roles.Where(r => r.BirthYear >= query.BirthYearFrom);
        }
        if (query.BirthYearTo is not null)
        {
            roles = roles.Where(r => r.BirthYear <= query.BirthYearTo);
        }

        var total = await roles.CountAsync(ct);
        var items = await roles
            .OrderBy(r => r.Company!.Name)
            .ThenBy(r => r.RoleType)
            .ThenBy(r => r.Id) // a unique last key: every page is stable
            .Skip((query.Page - 1) * (query.PageSize))
            .Take(query.PageSize)
            .Select(r => new RoleSearchItemDto(
                r.Company!.Orgnr, r.Company!.Name, r.RoleType, RoleTypes.NameOf(r.RoleType),
                r.PersonName, r.BirthYear, r.UnitOrgnr, r.UnitName, r.Resigned) )
            .ToListAsync(ct);

        return new PageResult<RoleSearchItemDto>(items, query.Page, query.PageSize, total);
    }

    // "DAGL, lede,,MEDL" → ["DAGL", "lede", "MEDL"]; null or blank → []
    private static List<string> SplitList(string? value) =>
        string.IsNullOrWhiteSpace(value)
            ? []
            : value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();
}
```

**What's new compared with step 4:**
- **`r.Company!.Name`: the navigation "upwards", without `Include`.** Using it in `OrderBy`/`Select` is
  enough; EF writes `FROM roles r INNER JOIN companies c ON r.orgnr = c.orgnr ORDER BY c.name, r.role_type, r.id`.
  **INNER** because every role has a company (required FK); 9c's was LEFT because a company may have no roles.
- **Person or unit in one filter:** `WHERE person_name ILIKE @p OR unit_name ILIKE @p`. One is always
  NULL and `NULL ILIKE` is never true. The `!` only quiets the compiler.
- **Birth years leave out units by themselves:** `birth_year >= 1970` is never true for NULL.
- **Comma-separated lists:** `Split` with `RemoveEmptyEntries | TrimEntries` (the `|` combines options)
  = PHP's `array_filter(array_map('trim', explode(',', $v)))`. `list.Contains(r.Column)` in a `Where`
  becomes `= ANY(@list)` (SQL's `IN`), as in step 7. **`Count > 0` check:** `?roleType= , ` splits
  into an empty list, and "one of []" matches nothing (0 rows); with the check the filter is skipped.
  Another common style: repeat the parameter (`?roleType=DAGL&roleType=LEDE`), which ASP.NET binds to
  a `List<string>` by itself, no `Split`.
- **`.Select(...).ToList()` on a list:** `SplitList` returns a `List`, but `Select` returns a lazy
  `IEnumerable` (a recipe, run when read), so `ToList()` makes it a list again, with `Count`.
  `Select` *is* the mapping (`array_map`). A list also has its own map that returns a list:
  `SplitList(query.RoleType).ConvertAll(t => t.ToUpperInvariant())`.
- **`ThenBy(r => r.Id)`, stable paging:** sorting only by company name has ties (7 roles per company),
  and tied rows may come in a different order on each query, so `Skip/Take` can repeat or skip a row
  between pages. A unique last key fixes the order.
- **No `RankOf` here:** this is a database query, and SQL can't call a C# method in `OrderBy`
  ("could not be translated"). `RoleTypes.NameOf` in the **final `Select`** is fine: EF runs it in C#
  on each row after the SQL.
- `CountAsync` before `OrderBy`/`Skip`: the count needs no JOIN and no sorting.
- `r.Company!.Orgnr` and `r.Orgnr` are the same value; `r.Orgnr` is already on the role.

```bash
curl -s "localhost:5020/api/roles?pageSize=3" | jq '.totalCount'                    # 26472
curl -s "localhost:5020/api/roles?name=hansen" | jq '.totalCount'                   # 514 (persons)
curl -s "localhost:5020/api/roles?name=revisjon" | jq '.totalCount'                 # 1409 (units)
curl -s "localhost:5020/api/roles?roleType=dagl,lede" | jq '.totalCount'            # 12256 = 5046 + 7210
curl -s "localhost:5020/api/roles?roleType=dagl&birthYearFrom=1970&birthYearTo=1975" | jq '.totalCount'  # 892
curl -si "localhost:5020/api/roles?pageSize=500" | head -1                          # 400
```
A raw `Ø` in a curl URL gives 400 (curl doesn't encode it); `curl -G --data-urlencode "roleType=FFØR"`
works, and browsers / `fetch` encode by themselves.

### For later: one role DTO for every endpoint
Separate DTOs per response shape (`RoleDto`, `RoleSearchItemDto`) make the "shape per endpoint" idea
clear while learning. In real projects, **one DTO that always returns everything** is common and fine:
9c would repeat the same orgnr and company name on each row, harmless. To hide a field where it's empty:
```csharp
[JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
public string? CompanyName { get; init; }
```
Leave it null in 9c and it disappears from the JSON. The cost: the field becomes optional in the
frontend type, and the contract no longer says what each endpoint returns. A practice3 idea.

---

## Step 10: the `users` table

**Why:** step 11 logs users in and gives them a JWT with their scopes. They need somewhere to live first.
There's no sign-up endpoint: users are created by hand with a hashed password.

**`Data/User.cs`:**
```csharp
public class User
{
    public int Id { get; set; }
    public required string Username { get; set; }
    public required string PasswordHash { get; set; }

    // Space-separated, like OAuth: "companies.read companies.import"
    public required string Scopes { get; set; }
}
```
- My first idea was `id, name, pass_hash, scope`. Two renames: **`username`**, since it's the login
  identifier and must be unique ("name" sounds like a display name, "Kari Nordmann"), and **`scopes`**,
  since a user can have several, space-separated the way OAuth writes them.

**`AppDbContext`:**
```csharp
public DbSet<User> Users => Set<User>();

modelBuilder.Entity<User>(user =>
{
    user.HasIndex(u => u.Username).IsUnique();
    user.Property(u => u.Username).HasMaxLength(50);
    user.Property(u => u.Scopes).HasMaxLength(200);
});
```
`PasswordHash` has no max length, so it's `text`: the length depends on the hash format, and a longer
format later must not be cut off.

```bash
dotnet ef migrations add AddUser -o Data/Migrations
dotnet ef database update
```
```
\d users
 id            | integer                | not null | generated by default as identity
 username      | character varying(50)  | not null |
 password_hash | text                   | not null |
 scopes        | character varying(200) | not null |
Indexes:
    "pk_users" PRIMARY KEY, btree (id)
    "ix_users_username" UNIQUE, btree (username)
```

### Why a hash, not encryption
> **A hash can't be reversed. Encryption can, by anyone with the key.** A leaked key or config = every
> password at once. With a hash, login hashes what the user typed and **compares**.

Two more things a *password* hash does (a plain SHA-256 does neither):
- **Salted:** a random salt per hash, stored inside the hash string. The same password gives a
  different hash every time (`secret123` twice: `AQAAAAIAAYagAAAAEFZA...` and `AQAAAAIAAYagAAAAEAow...`),
  so equal passwords don't show, and precomputed rainbow tables are useless.
- **Slow on purpose:** PBKDF2 with 100,000 iterations (`AYag` is 100,000 in base64). One login doesn't
  notice; an attacker trying billions of guesses does.

PHP: `password_hash()` / `password_verify()`. The hash stores its own format, salt and iteration count,
like bcrypt's `$2y$10$...`. In C#: `PasswordHasher<T>.HashPassword` / `VerifyHashedPassword` (step 11).

### The hash tool: `practice2/tools/hash-password.cs`
```csharp
#:package Microsoft.Extensions.Identity.Core@10.0.13

using Microsoft.AspNetCore.Identity;

if (args.Length != 1)
{
    Console.Error.WriteLine("Usage: dotnet run hash-password.cs -- <password>");
    return 1;
}

var hasher = new PasswordHasher<object>();
Console.WriteLine(hasher.HashPassword(new object(), args[0]));
return 0;
```
- A **.NET 10 file-based app**: one `.cs` file, no project, run like `php script.php`:
  `dotnet run hash-password.cs -- secret123`.
- `#:package` = a `<PackageReference>` in a `.csproj`. No `class` or `Main`: top-level statements.
- `PasswordHasher<object>`: `T` is the user type, which the default hasher ignores, so any type works.

### The users, inserted with psql
```sql
INSERT INTO users (username, password_hash, scopes) VALUES
  ('admin',  '<hash from the tool>', 'companies.read companies.import'),
  ('reader', '<another hash>',       'companies.read');
```
```
 id | username |             scopes              |      hash      | len
  1 | admin    | companies.read companies.import | AQAAAAIAAYagAA |  84
  2 | reader   | companies.read                  | AQAAAAIAAYagAA |  84
```
Two users with different scopes, so step 11d can show `reader` getting **403** on `POST /api/imports`.
**Passwords never go in git**, not even demo ones: only the hashes are in the database.

**Interview line:** "Passwords are stored as salted, slow PBKDF2 hashes via ASP.NET's `PasswordHasher`,
never encrypted; login re-hashes and compares. Scopes are stored per user and go into the JWT."

---

## Step 11a: `JwtOptions`, the typed JWT settings

**Why:** step 11 logs users in and returns a **JWT**: a signed token carrying `sub`, `name` and the
user's `scope`s. The API checks the signature on every request, with no database lookup. 11a is only the
settings (issuer, audience, signing key, lifetime), and the app **refuses to start** when they're wrong.

```bash
dotnet add package Microsoft.AspNetCore.Authentication.JwtBearer --version 10.0.13
```
It *validates* tokens (11d) and brings the `JsonWebTokenHandler` that *creates* them (11b).

**`Auth/JwtOptions.cs`:**
```csharp
public class JwtOptions
{
    public const string Section = "Jwt";

    [Required]
    public string Issuer { get; set; } = "";

    [Required]
    public string Audience { get; set; } = "";

    [Required, MinLength(32)]
    public string SigningKey { get; set; } = "";

    [Range(1, 1440)]
    public int LifetimeMinutes { get; set; } = 120;
}
```
- The **options pattern**: one config section becomes a typed, validated class. Laravel's
  `config('jwt.issuer')`, but a class instead of loose array keys.
- `MinLength(32)`: HMAC-SHA256 needs a key of at least 256 bits (32 bytes).
- `Section`: the string `"Jwt"` is written once.

**`appsettings.json`: everything except the key**
```json
"Jwt": {
  "Issuer": "practice2-api",
  "Audience": "practice2-api",
  "LifetimeMinutes": 120
}
```
Issuer = who made the token, audience = who it's for. The same API here.

**The key: user-secrets**, a `.env` outside the repo:
```bash
dotnet user-secrets init                     # adds <UserSecretsId> to the .csproj, once
dotnet user-secrets set "Jwt:SigningKey" "$(openssl rand -base64 48)"
dotnet user-secrets list
```
- Stored in `~/.microsoft/usersecrets/<id>/secrets.json`, so it can't be committed. The id is only that
  folder's name: it stays the same, and it's safe to commit.
- Every `openssl rand` gives a **new** key. Tokens signed with the old key stop working: that's how you
  rotate the key, or log everyone out at once.
- **Not a vault:** a plain JSON file, loaded only in Development. In production the key comes from an
  environment variable (`Jwt__SigningKey`, where `__` is the section separator) or a secret store.
- Config is layered: appsettings.json → appsettings.{Environment}.json → user-secrets → environment
  variables. Later sources win.

**`Program.cs`:**
```csharp
builder.Services.AddOptions<JwtOptions>()
    .Bind(builder.Configuration.GetSection(JwtOptions.Section))
    .ValidateDataAnnotations()
    .ValidateOnStart();
```
```
appsettings.json  "Jwt": { "Issuer", "Audience", "LifetimeMinutes" }
user-secrets      "Jwt:SigningKey"
                      ↓ merged into one "Jwt" section
JwtOptions        Issuer, Audience, LifetimeMinutes, SigningKey
```
- `GetSection("Jwt")`: that section from **all sources, merged**, which is why the secret's key lands
  next to appsettings' values.
- `Bind`: a key fills the property with the same name (**case-insensitive**).
- `AddOptions<JwtOptions>()`: registers it in DI; `TokenService` (11b) asks for `IOptions<JwtOptions>`.
- `ValidateDataAnnotations` runs the attributes; `ValidateOnStart` runs them **at boot**.
- PHP: `new JwtOptions(...config('jwt'))`, built once, validated, injected.

Checked: with the key removed, `dotnet run` crashes in `StartupValidator.Validate()` (exit code 134);
with it set again, the app starts.

### Why the key matters, and why the app fails at startup
- **Whoever has the key can forge a token** for any user with any scope, without a password. HMAC is
  *symmetric*: one key signs and verifies.
- **Fail fast:** a missing key breaks the deploy in the first second. Otherwise the app looks healthy and
  the first login gets a 500 hours later.
- **The expiry matters more for a JWT:** nothing is looked up, so a stolen token can't be revoked. Its
  lifetime limits the damage.

**Interview line:** "JWT settings are a typed options class, validated at startup. The signing key is in
user-secrets locally and an environment variable in production, never in the repo."

---

## Step 11b: `TokenService`, which creates the JWT

**Why:** 11c's login needs one call that turns a `User` into a signed token. The service builds the
claims, sets the expiry and signs, using 11a's `JwtOptions`.

**`Contracts/LoginResponse.cs`:**
```csharp
public record LoginResponse(string Token, DateTimeOffset ExpiresAt, string Username, string[] Scopes);
```
The frontend gets the expiry and the scopes next to the token, so it can hide the Import button without
decoding the token.

**`Auth/TokenService.cs`:**
```csharp
public class TokenService(IOptions<JwtOptions> options)
{
    // Used here to sign, and in 11d to validate: the same key both ways (HMAC is symmetric).
    public static SymmetricSecurityKey CreateSigningKey(JwtOptions jwt) =>
        new(Encoding.UTF8.GetBytes(jwt.SigningKey));

    public LoginResponse CreateToken(User user)
    {
        var jwt = options.Value;
        var scopes = user.Scopes.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var expiresAt = DateTimeOffset.UtcNow.AddMinutes(jwt.LifetimeMinutes);

        var claims = new List<Claim>
        {
            new("sub", user.Id.ToString()),
            new("name", user.Username),
        };
        claims.AddRange(scopes.Select(scope => new Claim("scope", scope)));

        var token = new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
        {
            Issuer = jwt.Issuer,
            Audience = jwt.Audience,
            Subject = new ClaimsIdentity(claims),
            Expires = expiresAt.UtcDateTime,
            SigningCredentials = new SigningCredentials(CreateSigningKey(jwt), SecurityAlgorithms.HmacSha256)
        });

        return new LoginResponse(token, expiresAt, user.Username, scopes);
    }
}
```
- **A claim** is one fact about the user, a type + value pair. `sub` (subject) = the user id, a standard
  JWT name. **One `scope` claim per scope**, so 11d can ask "is there a `scope` claim `companies.import`?".
- `claims.AddRange(scopes.Select(...))` = JS `claims.push(...scopes.map(s => ...))`.
- `Expires` becomes the `exp` claim (a Unix timestamp); validation rejects the token after it.
- PHP: `firebase/php-jwt`'s `JWT::encode($payload, $key, 'HS256')`, with typed claims.

**`Program.cs`:** `builder.Services.AddSingleton<TokenService>();`

### What's inside a JWT
My TokenService, run in a scratch app with a throwaway key, then decoded **without the key**:
```
eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9 . eyJhdWQiOiJwcmFjdGljZTIt... . Gpg_9vlwZgCmwS8NT3WSzq2z...

part 1 → {"alg":"HS256","typ":"JWT"}
part 2 → {"aud":"practice2-api","iss":"practice2-api","exp":1794133423,"sub":"1","name":"admin",
          "scope":["companies.read","companies.import"],"iat":1794126223,"nbf":1794126223}
```
- **header.payload.signature.** The header says how it was signed; the payload holds the claims; the
  signature is HMAC-SHA256 of `header.payload` with the key.
- > **The payload is not encrypted, only base64url-encoded.** Anyone with the token can read it (jwt.io).
  > The key gives **integrity**, not secrecy: without it nobody can change or create a token. So **no
  > secrets in the claims.** Like a signed (not encrypted) Laravel cookie.
- The two `scope` claims became one JSON array: the handler merges claims of the same type. Validation
  splits them back into two claims (11d).
- `iat` = issued at, `nbf` = not valid before. The handler adds them itself.

### Revoking: the JWT trade-off
The token says `companies.import` until its `exp`, even if the scope is removed in the database: the API
never asks the database. The usual fixes: a short access token (e.g. 15 min) plus a refresh token that
*is* checked against the database, or a lookup per request (and the "stateless" benefit is gone).

### Why a singleton
Not because it's "called at any time": scoped services are too. `TokenService` keeps **no per-request
state** and depends only on singletons (`IOptions<T>`). The rule the other way: a singleton must never
take a scoped service like `AppDbContext` (a captive dependency, concept 1). So in 11c the **controller**
gets the DbContext and passes only the `User` to `TokenService`.

### Two ways to write `new`
Neither is an anonymous object; both are normal constructor calls.
```csharp
// Target-typed new (C# 9): the type comes from where the value goes, here the return type.
public static SymmetricSecurityKey CreateSigningKey(JwtOptions jwt) =>
    new(Encoding.UTF8.GetBytes(jwt.SigningKey));      // = new SymmetricSecurityKey(...)

// Object initializer (C# 3): the parameterless constructor, then property assignments.
var d = new SecurityTokenDescriptor { Issuer = jwt.Issuer, Audience = jwt.Audience };
// = var d = new SecurityTokenDescriptor(); d.Issuer = jwt.Issuer; d.Audience = jwt.Audience;
```
| Form | Meaning |
|---|---|
| `new X(a, b)` | constructor arguments |
| `new(a, b)` | the same, with the type taken from the target |
| `new X { P = a }` | empty constructor, then property assignments |
| `new X(a) { P = b }` | both |
| `new { Name = "x" }` | an **anonymous** object: no type name, read-only, local only |

- `new("sub", ...)` inside `new List<Claim> { ... }` is `new Claim(...)`: the list says the type.
- **An initializer still needs setters.** It assigns the properties, so they need `set` (or `init`, C# 9:
  settable only in the initializer). What it removes is the constructor that only copies its parameters
  into properties. `required` (C# 11) makes the compiler check that you set them in the `{ }`.
- JS: `Object.assign(new X(), { issuer, audience })`. PHP has no equivalent; `new X(issuer: ...)` named
  arguments still go through the constructor.
- A class with many optional settings (`SecurityTokenDescriptor` has about 15) offers a parameterless
  constructor + settable properties, and you set only what you need.

**Interview line:** "A JWT is signed, not encrypted: the signature proves nobody changed the claims, so
no secrets go in them. The token carries the scopes, so it can't be revoked before it expires; keep it
short-lived."

---

## Step 11c: `POST /api/auth/login`

**Why:** the first endpoint that uses steps 10, 11a and 11b together: find the user, check the password
against the hash, return 11b's token.

**`Contracts/LoginRequest.cs`:**
```csharp
using System.ComponentModel.DataAnnotations;

public record LoginRequest([Required] string Username, [Required] string Password);
```
`[Required]`, `[Range]`, `[MinLength]` are classes in `System.ComponentModel.DataAnnotations`
(`RequiredAttribute`; the `Attribute` suffix is dropped when you use one). The `using` makes them
visible. An attribute does nothing by itself; something reads it: `[ApiController]` for request models
(400), `ValidateDataAnnotations()` for options (11a), EF for max lengths. PHP 8: `#[Assert\NotBlank]`.

**`Program.cs`:**
```csharp
builder.Services.AddSingleton<IPasswordHasher<User>, PasswordHasher<User>>();
```
- Two type parameters: **interface → implementation**. "Whoever asks for `IPasswordHasher<User>` gets a
  `PasswordHasher<User>`." The controller depends on the interface, so a test can pass in a fake.
- **Singleton** for the same reason as `TokenService`: no per-request state, nothing scoped inside, safe
  to share between threads. Scoped would work too, just with one new object per request for nothing.
- A singleton is an **object**, not a thread: built the first time something asks for it, kept until the
  app stops, and used by many requests (on many threads) at once. That's why it must be stateless.
- No package needed: it's in ASP.NET Core, the same class as the hash tool.

**`Controllers/AuthController.cs`:**
```csharp
[ApiController]
[Route("api/auth")]
public class AuthController(AppDbContext db, IPasswordHasher<User> hasher, TokenService tokens) : ControllerBase
{
    [HttpPost("login")]
    public async Task<ActionResult<LoginResponse>> Login(LoginRequest request, CancellationToken ct)
    {
        var user = await db.Users.AsNoTracking()
            .FirstOrDefaultAsync(u => u.Username == request.Username, ct);

        // The same answer for an unknown user and a wrong password, so callers can't find out which usernames exist.
        if (user is null || hasher.VerifyHashedPassword(user, user.PasswordHash, request.Password) == PasswordVerificationResult.Failed)
            return Problem(statusCode: StatusCodes.Status401Unauthorized, title: "Wrong username or password");

        return tokens.CreateToken(user);
    }
}
```
- `LoginRequest request` without `[FromBody]`: `[ApiController]` reads a complex type from the JSON body.
- `user is null || ...`: `||` stops early, so `user.PasswordHash` is never read on a null user.
- `VerifyHashedPassword` returns `Success`, `Failed` or `SuccessRehashNeeded` ("right, but hashed with
  older settings: save a new hash"). So the check is `== Failed`, not `!= Success`.
- The controller gets the scoped DbContext and gives the singleton `TokenService` only the `User`.

### The four questions
- **Unknown user vs wrong password: the same 401.** Different answers ("no such user") let anyone test
  which usernames exist (*user enumeration*), and then guess passwords only for those.
- **How the check works without decrypting:** the stored hash contains its salt and iteration count.
  `VerifyHashedPassword` hashes the attempt **with that same salt** and compares. (A new random salt is
  only for *new* hashes, which is why the same password gave two different hashes in step 10.)
- **`Admin` vs `admin`:** Postgres `=` is case-sensitive, so `Admin` gets 401. Usernames are usually
  case-insensitive: store them lower-case and normalise the input (`Trim().ToLowerInvariant()`), the same
  lesson as `orgForm` in step 4. Passwords are always case-sensitive.
- **Why POST:** not for encryption; **HTTPS** encrypts GET and POST alike. A query string ends up in server
  access logs, proxy logs, browser history and the `Referer` header; a body doesn't. And a login creates
  something (a token), which isn't a GET's job.

### Checked
Run on a spare port with a temporary user, deleted afterwards:
```
wrong password  401 {"title":"Wrong username or password","status":401,...}
unknown user    401 (the same body)
{}              400 {"Password":["The Password field is required."],"Username":[...]}
Tmp-Verify      401 (case-sensitive)
right password  200 {"token":"eyJhbGciOiJIUzI1NiIs...","expiresAt":"...","username":"tmp-verify","scopes":[...]}
```
Paste the token into jwt.io to see the claims from 11b.

**Interview line:** "Login looks the user up, verifies the PBKDF2 hash, and returns the same 401 for an
unknown user and a wrong password, so usernames can't be enumerated."

---

## Step 11d: validate the JWT, and scope policies

**Why:** 11c hands out tokens; now the API checks them on every request (authentication: who are you?)
and checks the scopes per endpoint (authorisation: what may you do?).

**`Program.cs`, authentication:**
```csharp
builder.Services.AddAuthentication().AddJwtBearer();
builder.Services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
    .Configure<IOptions<JwtOptions>>((bearer, jwt) =>
    {
        bearer.MapInboundClaims = false;
        bearer.TokenValidationParameters = new TokenValidationParameters
        {
            ValidIssuer = jwt.Value.Issuer,
            ValidAudience = jwt.Value.Audience,
            IssuerSigningKey = TokenService.CreateSigningKey(jwt.Value)
        };
    });
```
- `AddJwtBearer()` reads `Authorization: Bearer <token>` and checks the **signature, issuer, audience and
  expiry**. All four are on by default; you only give the values to check against.
- `Configure<IOptions<JwtOptions>>`: "when the bearer options are built, give me our validated
  `JwtOptions`". It reads the config **late**, so step 13's tests can swap in their own key.
- `MapInboundClaims = false`: keeps `sub`, `name`, `scope` as they are, instead of .NET's old long URIs.
- Expiry has a 5-minute `ClockSkew` by default: a token works up to 5 minutes after `exp`.

**`Program.cs`, authorisation:**
```csharp
builder.Services.AddAuthorizationBuilder()
    .AddPolicy("companies.read", policy => policy.RequireClaim("scope", "companies.read"))
    .AddPolicy("companies.import", policy => policy.RequireClaim("scope", "companies.import"));
```
A **policy** is a named rule. The token's `"scope":[...]` array is split back into one claim per scope.

**The pipeline:**
```csharp
app.UseAuthentication();
app.UseAuthorization();
...
app.MapControllers();
```
Authentication before authorisation, and both before the endpoints. Laravel: `auth` before `can:...`.

**The controllers:** `[Authorize(Policy = "companies.read")]` on `CompaniesController` and
`RolesController`, `[Authorize(Policy = "companies.import")]` on `ImportsController`. `AuthController`
stays open: you can't need a token to get a token.

**Login, case-insensitive:**
```csharp
var username = request.Username.Trim().ToLowerInvariant();
var user = await db.Users.AsNoTracking()
    .FirstOrDefaultAsync(u => u.Username == username, ct);
```
Usernames are stored lower-case; `"  TMP-Reader "` logs in as `tmp-reader`.

### Checked
On a spare port, with two temporary users (deleted afterwards):
```
no token  /api/companies 401 | /api/roles 401 | /api/imports 401
reader    /api/companies 200 | /api/roles 200 | /api/imports 403 | POST /api/imports 403
admin     /api/imports 200
tampered token → 401  WWW-Authenticate: Bearer error="invalid_token"
```
I forgot `[Authorize]` on `RolesController` at first: `/api/roles` answered 200 without a token. An
endpoint you forget is **open**. A fallback policy turns that around (11e).

### 401 vs 403
| Case | Status |
|---|---|
| No token, expired token, changed token | 401: not authenticated |
| Valid token, missing scope (`reader` on imports) | 403: authenticated, not allowed |

### Where the API gets the key to check
It created the tokens itself, so it has the key: `IssuerSigningKey = TokenService.CreateSigningKey(...)`,
the same `JwtOptions.SigningKey` 11b signs with. (`UserSecretsId` is only the folder where *development*
keeps it; production would use an environment variable.)

**Maskinporten is different:** **RS256**, asymmetric. Maskinporten signs with its **private** key; the API
validates with the **public** key from Maskinporten's JWKS URL. The API can check tokens but never create
one. With HMAC, whoever can check can also forge.

### Stateless singletons, again
Parameters and local variables exist **once per call**, on that call's own thread stack, so two logins at
once can't see each other's. Fields live on the object, and a singleton has one object: shared. A
stateless singleton = everything is a parameter or a local, plus read-only fields. JS: a function's
`let`s are per call; a module-level `let` is shared.

### Does order matter in `Program.cs`?
- **`builder.Services.Add...`: almost never.** A list of recipes; nothing is built until `Build()` or the
  first time it's asked for. The exception: the same interface registered twice, and the last one wins.
- **`app.Use...`: always.** The middleware is a pipeline, and each request goes through it top to bottom.
- Laravel: registering service providers vs the order of the middleware stack.

**Interview line:** "JwtBearer validates signature, issuer, audience and expiry; scope policies on the
controllers give 401 without a valid token and 403 without the scope."

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
| 9b | `-o Date/Migrations`, then deleted the folder by hand | The snapshot kept the change, so the next `add` was empty. Use `migrations remove`: it reverts the snapshot too |
| 9b | `await Task.WhenAll(calls).ToDictionaryAsync()` | `ToDictionaryAsync` is EF's, for database queries. `WhenAll` returns a plain array; key by orgnr by making `calls` a dictionary |
| 9c | `Names[code] ?? code` | A C# dictionary indexer throws for a missing key; `??` never runs. Use `GetValueOrDefault(code, code)` |
| 9c | `("Bostyrer")` in a `(string Name, int Rank)` dictionary; `GetValueOrDefault(code, code)` | A tuple value needs both parts; the fallback must have the value's type, so pick `.Name` / `.Rank` |
| 9d | `?roleType= , ` gave 0 rows | A non-blank string can split into an empty list; check `Count > 0` before filtering |
| 11a | `"LifeTimeMinutes"` in appsettings.json for `LifetimeMinutes` | It still bound: config keys are case-insensitive. Match the spelling anyway, so a search finds both |
| 11b | Thought the JWT payload is encrypted and only the key holder can read the scopes | It's base64url: anyone can read it. The key only stops changes and forgeries. No secrets in claims |
| 11c | "POST protects the login data" | HTTPS encrypts GET and POST alike. POST keeps the password out of URLs, which end up in logs, history and `Referer` |
| 11d | No `[Authorize]` on `RolesController` | A forgotten endpoint is open: `/api/roles` gave 200 without a token. A fallback policy closes forgotten endpoints |
| 11d | "The API finds the key through `UserSecretsId`" | That's only where development stores it. The API signs and checks with the same `JwtOptions.SigningKey` |

---

## What comes next

### The rounds
| Round | Folder | How |
|---|---|---|
| practice | `practice/` | Done (Oct–Nov 2026). Guided by an AI tutor, I typed every line. |
| practice2 | `practice2/` | Guided again, with the new `roles` table, and the login split into small steps. |
| practice3 | `practice3/` | Fewer hints, persons derived from roles, timed drills. |
| A full project | a new repo | When I'm fluent: a complete project with Docker and CI/CD, from [COMPANY-REGISTRY-NOTES.md](COMPANY-REGISTRY-NOTES.md), without AI. |

### practice2: React
| Step | What | New in this round |
|---|---|---|
| 1 | Create the app (Vite, React, TypeScript), Prettier, and a proxy to the API | Proxy without tokens from the start |
| 2 | Login page: form, `POST /api/auth/login`, save the session in `localStorage` | from the old step 11 |
| 3 | `apiFetch` sends the token, and a typed API client | from the old step 11 |
| 4 | Search form with loading and error states | |
| 5 | Paging, with a component and props | |
| 6 | Router and the company page (400/404) | |
| 7 | **Roles on the company page** | new |
| 8 | Search kept in the URL | |
| 9 | Layout route (header, footer, `<Outlet />`) | |
| 10 | Auth context, route guard, username and Log out in the header | from the old step 11 |
| 11 | Token expiry: automatic logout, and a 401 sends you to the login page | from the old step 11 |
| 12 | Import page with polling (link only for users with `companies.import`) | |

### practice3 (ideas)
- **Persons derived from roles:** a `persons` table, and roles that point to it.
  - The hard part: Brreg gives no national ID number, so the same person can only be matched by name +
    birth date, and that isn't always unique. A real data-modelling problem.
  - Search a person and see all their companies.
- Fewer hints: I write each step first, then compare with the guide.
- Timed drills, 45 minutes each: a CRUD endpoint with EF Core and a test; a React page that fetches,
  lists and filters.
- Mock interviews: a task without code, then a review like an interviewer would give.

### practice4 (ideas)
- **History tables:** when a role appeared or disappeared, and a company's earlier names. Brreg gives no
  start or end dates for roles (only `avregistrert`), so the importer records when it *saw* a change.
