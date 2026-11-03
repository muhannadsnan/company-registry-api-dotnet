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
