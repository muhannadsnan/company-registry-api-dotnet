# .NET Web API: how it is built (company-registry-api-dotnet)

ASP.NET Core (.NET 10 LTS) · Entity Framework Core · PostgreSQL · xUnit · Testcontainers · Docker · GitHub Actions.
Every file a developer writes by hand, with notes. The code blocks are copied from the repo (23 Oct 2026).

Repo: https://github.com/muhannadsnan/company-registry-api-dotnet
Live: https://company-registry-api-dotnet-latest.onrender.com (frontend) · /docs (API reference)

---

## 1. Setup commands

```bash
# The .NET SDK (the runtime alone can only run apps, not build them)
sudo apt-get install -y dotnet-sdk-10.0
dotnet --list-sdks

# Solution, API project, test project
dotnet new globaljson --sdk-version 10.0.100 --roll-forward latestFeature   # pin the SDK
dotnet new sln -n CompanyRegistry                                         # the .slnx groups projects
dotnet new webapi -n CompanyRegistry.Api -o src/CompanyRegistry.Api --use-controllers
dotnet new xunit  -n CompanyRegistry.Tests -o tests/CompanyRegistry.Tests
dotnet sln add src/CompanyRegistry.Api tests/CompanyRegistry.Tests
dotnet add tests/CompanyRegistry.Tests reference src/CompanyRegistry.Api

# NuGet packages (like composer require)
dotnet add src/CompanyRegistry.Api package Npgsql.EntityFrameworkCore.PostgreSQL
dotnet add src/CompanyRegistry.Api package Microsoft.EntityFrameworkCore.Design
dotnet add src/CompanyRegistry.Api package EFCore.NamingConventions
dotnet add src/CompanyRegistry.Api package Microsoft.AspNetCore.Authentication.JwtBearer
dotnet add src/CompanyRegistry.Api package Microsoft.Extensions.Http.Resilience
dotnet add src/CompanyRegistry.Api package Microsoft.Extensions.Diagnostics.HealthChecks.EntityFrameworkCore
dotnet add src/CompanyRegistry.Api package Scalar.AspNetCore
dotnet add tests/CompanyRegistry.Tests package Microsoft.AspNetCore.Mvc.Testing
dotnet add tests/CompanyRegistry.Tests package Testcontainers.PostgreSql

# EF Core command-line tool, pinned in the repo (dotnet-tools.json)
dotnet new tool-manifest
dotnet tool install dotnet-ef
dotnet tool restore                      # after cloning
```

**Database and migrations**

```bash
docker compose up -d                                                   # Postgres on localhost:5433
dotnet ef migrations add InitialCreate -p src/CompanyRegistry.Api -o Data/Migrations
dotnet ef migrations script -p src/CompanyRegistry.Api                 # show the SQL
dotnet ef database update -p src/CompanyRegistry.Api                   # apply to the database
dotnet ef migrations has-pending-model-changes -p src/CompanyRegistry.Api   # entity changed, migration forgotten?
dotnet ef migrations remove -p src/CompanyRegistry.Api                 # undo the last, unapplied migration
```

**Run, test, check**

```bash
dotnet run   --project src/CompanyRegistry.Api     # build once and run (http://localhost:5080)
dotnet watch --project src/CompanyRegistry.Api     # rebuild/hot-reload on changes
dotnet test                                        # all tests (Docker must run: Testcontainers)
dotnet build -c Release -warnaserror               # what CI does
dotnet format --verify-no-changes                  # code style check
dotnet list package --vulnerable --include-transitive
```

**Docker and deploy**

```bash
docker build -t company-registry-api:local .
docker run --rm -p 8090:8080 --env-file render.env company-registry-api:local
gh secret set RENDER_DEPLOY_HOOK_URL --repo muhannadsnan/company-registry-api-dotnet
```

**Get a token and call the API**

```bash
TOKEN=$(curl -s -X POST localhost:5080/connect/token \
  -d grant_type=client_credentials -d client_id=demo-importer -d client_secret=importer-secret | jq -r .access_token)
curl -s -X POST localhost:5080/api/imports -H "Authorization: Bearer $TOKEN"        # 202 Accepted
curl -s "localhost:5080/api/companies?name=bil" -H "Authorization: Bearer $TOKEN"
```

Environment variables override config: `Import__PageSize=200` = `"Import": { "PageSize": 200 }`
(`__` replaces `:`, because Linux variable names can't contain `:`).

---

## 2. Folder structure

```
CompanyRegistry.slnx              solution: lists the projects
global.json                       pins the SDK version
dotnet-tools.json                 pins dotnet-ef
docker-compose.yml                local Postgres on 5433
Dockerfile                        three-stage image: Node builds React, SDK builds the API, runtime runs both
.github/workflows/ci.yml          CI/CD
src/CompanyRegistry.Api/
  CompanyRegistry.Api.csproj      target framework + NuGet packages
  appsettings.json                config for every environment
  appsettings.Development.json    overrides for local development
  Properties/launchSettings.json  dotnet run profile: port, environment
  Program.cs                      DI registrations + middleware pipeline
  Domain/                         business rules with no framework (MOD11)
  Data/                           entities, DbContext, migrations (database shape)
  Contracts/                      DTOs (API shape, the public promise)
  Controllers/                    HTTP endpoints
  Auth/                           scopes, policies, dev token issuer
  Import/                         Brreg client, importer, queue, background services
  Infrastructure/                 middleware, OpenAPI tweak
tests/CompanyRegistry.Tests/
  Infrastructure/                 test host (WebApplicationFactory) + fake Brreg
  *Tests.cs                       unit and integration tests
web/                              React + TypeScript frontend (see REACT-NOTES.md)
My Learning/                      these notes
```

**PHP/CodeIgniter to .NET**

| CodeIgniter / PHP | ASP.NET Core |
|---|---|
| `composer.json` | `.csproj` (`PackageReference`) |
| `config/*.php` + `.env` | `appsettings*.json` + environment variables, bound to `IOptions<T>` |
| `$this->load->model()` | constructor injection from the DI container |
| hooks | middleware (`app.Use…`) |
| controller methods + routes.php | controller classes with `[Route]`, `[HttpGet]` attributes |
| Query Builder + migrations | EF Core: `DbContext`, LINQ, migrations |
| cron job | `BackgroundService` (+ `PeriodicTimer`) |
| PHPUnit | xUnit (`[Fact]`, `[Theory]`) |

---

## 3. Configuration files
### global.json

Pins the SDK: 10.0.100 or any newer 10.0.x feature band. Everyone (and CI) builds with the same major SDK.

```json
{
  "sdk": {
    "version": "10.0.100",
    "rollForward": "latestFeature"
  }
}
```

### docker-compose.yml

- Its own Postgres on host port **5433** (5432 belongs to project A).
- `pgdata` volume: data survives `docker compose down` (not `down -v`).
- `healthcheck`: Docker knows when Postgres is ready.

```yaml
name: company-registry

services:
  db:
    image: postgres:17-alpine
    environment:
      POSTGRES_DB: companies
      POSTGRES_USER: companies
      POSTGRES_PASSWORD: companies
    ports:
      # 5433 on the host, so it can run next to another local Postgres on 5432
      - "5433:5432"
    volumes:
      - pgdata:/var/lib/postgresql/data
    healthcheck:
      test: ["CMD-SHELL", "pg_isready -U companies -d companies"]
      interval: 5s
      timeout: 3s
      retries: 10

volumes:
  pgdata:
```

### src/CompanyRegistry.Api/CompanyRegistry.Api.csproj

- `Microsoft.NET.Sdk.Web`: an ASP.NET Core project.
- `Nullable enable`: `string` = never null, `string?` = may be null; the compiler warns.
- `ImplicitUsings`: common `using` lines (System, Linq, …) are added automatically.
- `EntityFrameworkCore.Design` is `PrivateAssets=all`: needed by the `dotnet ef` tool, not shipped.

```xml
<Project Sdk="Microsoft.NET.Sdk.Web">

  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="EFCore.NamingConventions" Version="10.0.1" />
    <PackageReference Include="Microsoft.AspNetCore.Authentication.JwtBearer" Version="10.0.13" />
    <PackageReference Include="Microsoft.AspNetCore.OpenApi" Version="10.0.13" />
    <PackageReference Include="Microsoft.EntityFrameworkCore.Design" Version="10.0.13">
      <IncludeAssets>runtime; build; native; contentfiles; analyzers; buildtransitive</IncludeAssets>
      <PrivateAssets>all</PrivateAssets>
    </PackageReference>
    <PackageReference Include="Microsoft.Extensions.Diagnostics.HealthChecks.EntityFrameworkCore" Version="10.0.13" />
    <PackageReference Include="Microsoft.Extensions.Http.Resilience" Version="10.11.0" />
    <PackageReference Include="Npgsql.EntityFrameworkCore.PostgreSQL" Version="10.0.3" />
    <PackageReference Include="Scalar.AspNetCore" Version="2.18.2" />
  </ItemGroup>

</Project>
```

### src/CompanyRegistry.Api/appsettings.json

- Base config for all environments. Logging as **JSON** to the console, with scopes (correlation id).
- `Import`: which Brreg data to fetch (Horten 3901 + Tønsberg 3905, AS + ASA).
- `Auth.SigningKey` is empty on purpose: it must come from the environment (dev file, or a secret in production).
  `ValidateOnStart` makes the app refuse to start without it.

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning",
      "Microsoft.EntityFrameworkCore.Database.Command": "Warning",
      "System.Net.Http.HttpClient": "Warning",
      "Polly": "Warning"
    },
    "Console": {
      "FormatterName": "json",
      "FormatterOptions": {
        "IncludeScopes": true,
        "TimestampFormat": "yyyy-MM-ddTHH:mm:ss.fffZ",
        "UseUtcTimestamp": true
      }
    }
  },
  "AllowedHosts": "*",
  "ConnectionStrings": {
    "Companies": "Host=localhost;Port=5433;Database=companies;Username=companies;Password=companies"
  },
  "Database": {
    "MigrateOnStartup": false
  },
  "Docs": {
    "Enabled": false
  },
  "Auth": {
    "Issuer": "company-registry-dev",
    "Audience": "company-registry-api",
    "SigningKey": "",
    "EnableDevTokenEndpoint": false,
    "Clients": []
  },
  "Import": {
    "BaseUrl": "https://data.brreg.no/",
    "MunicipalityNumbers": [ "3901", "3905" ],
    "OrganisationForms": [ "AS", "ASA" ],
    "PageSize": 500,
    "MaxRows": 10000,
    "ScheduleInterval": null
  }
}
```

### src/CompanyRegistry.Api/appsettings.Development.json

Loaded on top when `ASPNETCORE_ENVIRONMENT=Development`: migrate on start, docs page on,
the local token endpoint and two demo clients. Not secrets: development-only values.

```json
{
  "Database": {
    "MigrateOnStartup": true
  },
  "Docs": {
    "Enabled": true
  },
  "Auth": {
    "SigningKey": "local-development-signing-key-not-a-secret-0123456789",
    "EnableDevTokenEndpoint": true,
    "Clients": [
      {
        "ClientId": "demo-reader",
        "ClientSecret": "reader-secret",
        "Scopes": [ "companies.read" ]
      },
      {
        "ClientId": "demo-importer",
        "ClientSecret": "importer-secret",
        "Scopes": [ "companies.read", "companies.import" ]
      }
    ]
  }
}
```

### src/CompanyRegistry.Api/Properties/launchSettings.json

Used by `dotnet run` / `dotnet watch` only: port 5080, environment Development.

```json
{
  "$schema": "https://json.schemastore.org/launchsettings.json",
  "profiles": {
    "http": {
      "commandName": "Project",
      "dotnetRunMessages": true,
      "launchBrowser": false,
      "applicationUrl": "http://localhost:5080",
      "environmentVariables": {
        "ASPNETCORE_ENVIRONMENT": "Development"
      }
    }
  }
}
```

### Dockerfile

- **Stage 1 (node):** `npm ci` + `npm run build` turns the React app into static files (`web/dist`).
- **Stage 2 (sdk):** restore first (cached layer), then publish a Release build of the API.
- **Stage 3 (aspnet):** only the runtime + the published DLLs + the built frontend in `wwwroot`;
  runs as the non-root `app` user.
- No secrets inside the image: configuration comes from environment variables at start.

```dockerfile
# Frontend stage: build the React app into static files (web/dist).
FROM node:24-alpine AS web
WORKDIR /web
COPY web/package.json web/package-lock.json ./
RUN npm ci
COPY web/ ./
RUN npm run build

# Build stage: full SDK, restore first so the package layer is cached between builds.
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY global.json ./
COPY src/CompanyRegistry.Api/CompanyRegistry.Api.csproj src/CompanyRegistry.Api/
RUN dotnet restore src/CompanyRegistry.Api/CompanyRegistry.Api.csproj
COPY src/ src/
RUN dotnet publish src/CompanyRegistry.Api --configuration Release --no-restore --output /app

# Runtime stage: only the ASP.NET runtime and the published app, running as a non-root user.
FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app .
COPY --from=web /web/dist ./wwwroot
USER $APP_UID
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "CompanyRegistry.Api.dll"]
```

### .github/workflows/ci.yml

- **backend** (every push and PR): format check, vulnerable packages, build with warnings as errors,
  missing-migration check, tests (Testcontainers starts a real Postgres).
- **frontend** (in parallel): `npm ci`, ESLint, `npm audit`, type-checked production build.
- **deploy** (only `main`, only if both passed): build the image, push to ghcr.io tagged with the
  commit SHA, call Render's deploy hook with that exact tag. The hook URL is a GitHub secret.

```yaml
name: CI/CD

on:
  push:
    branches: [main]
  pull_request:

jobs:
  backend:
    name: "Backend: checks, build, tests"
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v5

      - uses: actions/setup-dotnet@v5
        with:
          dotnet-version: 10.0.x

      - name: Restore
        run: dotnet restore

      - name: Check formatting
        run: dotnet format --verify-no-changes --no-restore

      - name: Check for vulnerable packages
        run: |
          dotnet list package --vulnerable --include-transitive | tee vulnerable.txt
          ! grep -q "has the following vulnerable packages" vulnerable.txt

      - name: Build
        run: dotnet build --no-restore --configuration Release -warnaserror

      # Fails if an entity was changed without adding a migration for it.
      - name: Check for missing migrations
        run: |
          dotnet tool restore
          dotnet ef migrations has-pending-model-changes --project src/CompanyRegistry.Api --configuration Release --no-build

      # Integration tests start a throwaway Postgres with Testcontainers; the runner already has Docker.
      - name: Test
        run: dotnet test --no-build --configuration Release --logger "trx;LogFileName=results.trx" --results-directory test-results

      - name: Upload test results
        if: always()
        uses: actions/upload-artifact@v7
        with:
          name: test-results
          path: test-results

  frontend:
    name: "Frontend: lint, audit, build"
    runs-on: ubuntu-latest
    defaults:
      run:
        working-directory: web
    steps:
      - uses: actions/checkout@v5

      - uses: actions/setup-node@v7
        with:
          node-version: 24
          cache: npm
          cache-dependency-path: web/package-lock.json

      - name: Install
        run: npm ci

      - name: Lint
        run: npm run lint

      - name: Check for vulnerable packages
        run: npm audit --audit-level=high

      # Type-checks with tsc, then builds the production bundle.
      - name: Build
        run: npm run build

  # CD: only for pushes to main, and only after every check and test above has passed.
  deploy:
    name: "Deploy: image to ghcr.io, then Render"
    needs: [backend, frontend]
    if: github.event_name == 'push' && github.ref == 'refs/heads/main'
    runs-on: ubuntu-latest
    permissions:
      contents: read
      packages: write
    env:
      IMAGE: ghcr.io/${{ github.repository }}
    steps:
      - uses: actions/checkout@v5

      - name: Build and push the image to GitHub Container Registry
        run: |
          echo "${{ secrets.GITHUB_TOKEN }}" | docker login ghcr.io --username "${{ github.actor }}" --password-stdin
          docker build \
            --label "org.opencontainers.image.source=${{ github.server_url }}/${{ github.repository }}" \
            --tag "$IMAGE:${{ github.sha }}" --tag "$IMAGE:latest" .
          docker push --all-tags "$IMAGE"

      # Render pulls exactly the image built from this commit.
      - name: Deploy to Render
        env:
          RENDER_DEPLOY_HOOK_URL: ${{ secrets.RENDER_DEPLOY_HOOK_URL }}
        run: |
          if [ -z "$RENDER_DEPLOY_HOOK_URL" ]; then
            echo "::warning::RENDER_DEPLOY_HOOK_URL is not set; image pushed but not deployed."
            exit 0
          fi
          image_url=$(jq -rn --arg v "$IMAGE:${{ github.sha }}" '$v|@uri')
          curl --fail --silent --show-error --request POST "$RENDER_DEPLOY_HOOK_URL&imgURL=$image_url"
```

---

## 4. The application code

In the order a developer would write them: the rules, the data, the API, then the import.

### src/CompanyRegistry.Api/Program.cs

**The heart of the app.** Two halves:
1. **`builder.Services.Add…`** registers what the DI container can build (controllers, DbContext, auth,
   options, HttpClient, background services). Lifetimes: `AddSingleton` (one for the app), `AddScoped`
   (one per request), `AddHostedService` (runs in the background).
2. **`app.Use…` / `app.Map…`** is the middleware pipeline, in order: correlation id → error handling →
   authentication → authorization → controllers. Order matters (authorization needs authentication first).

Also: options bound and validated at startup, JWT validation, scope policies, a **fallback policy**
(secure by default), typed `HttpClient` with retries, health check, docs page by config, migrate on start.

**Serving the frontend:** if `wwwroot/index.html` exists (only in the Docker image), `UseStaticFiles`
serves the React files, and `MapFallbackToFile` sends React routes like `/companies/912855414` to
`index.html`. Its regex leaves `/api/…` and `/connect/…` alone, so unknown API paths never return the app.

```csharp
using CompanyRegistry.Api.Auth;
using CompanyRegistry.Api.Data;
using CompanyRegistry.Api.Import;
using CompanyRegistry.Api.Infrastructure;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

// ---- Services: what the DI container can build -------------------------------------------

builder.Services.AddControllers();
builder.Services.AddProblemDetails(o => o.CustomizeProblemDetails = ctx =>
    ctx.ProblemDetails.Extensions["correlationId"] = ctx.HttpContext.TraceIdentifier);
builder.Services.AddOpenApi(o => o.AddDocumentTransformer<BearerSecuritySchemeTransformer>());

builder.Services.AddDbContext<AppDbContext>(o => o
    .UseNpgsql(builder.Configuration.GetConnectionString("Companies"))
    .UseSnakeCaseNamingConvention());
builder.Services.AddHealthChecks().AddDbContextCheck<AppDbContext>("database");

// Auth: validate JWTs, then decide access per scope.
builder.Services.AddOptions<AuthOptions>()
    .Bind(builder.Configuration.GetSection(AuthOptions.Section))
    .ValidateDataAnnotations()
    .ValidateOnStart();
builder.Services.AddSingleton<DevTokenIssuer>();

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();
builder.Services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
    .Configure<IOptions<AuthOptions>>((jwt, auth) =>
    {
        // Keep claim names as they are in the token ("scope", "client_id") instead of mapping them to long URIs.
        jwt.MapInboundClaims = false;
        jwt.TokenValidationParameters = new TokenValidationParameters
        {
            ValidIssuer = auth.Value.Issuer,
            ValidAudience = auth.Value.Audience,
            IssuerSigningKey = DevTokenIssuer.SigningKey(auth.Value),
            ClockSkew = TimeSpan.FromSeconds(30),
        };
    });

builder.Services.AddAuthorizationBuilder()
    .AddPolicy(Scopes.CompaniesRead, p => p.RequireAuthenticatedUser().RequireScope(Scopes.CompaniesRead))
    .AddPolicy(Scopes.CompaniesImport, p => p.RequireAuthenticatedUser().RequireScope(Scopes.CompaniesImport))
    // Secure by default: an endpoint nobody remembered to annotate still needs a valid token.
    .SetFallbackPolicy(new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build());

// Import: typed HttpClient from IHttpClientFactory, with retries and timeouts from the resilience handler.
builder.Services.AddOptions<ImportOptions>()
    .Bind(builder.Configuration.GetSection(ImportOptions.Section))
    .ValidateDataAnnotations()
    .ValidateOnStart();
builder.Services.AddHttpClient<BrregClient>((sp, http) =>
    {
        http.BaseAddress = new Uri(sp.GetRequiredService<IOptions<ImportOptions>>().Value.BaseUrl);
        http.DefaultRequestHeaders.UserAgent.ParseAdd("company-registry-api-dotnet/1.0");
    })
    .AddStandardResilienceHandler();

builder.Services.AddSingleton<ImportQueue>();
builder.Services.AddScoped<ImportRunService>();
builder.Services.AddScoped<CompanyImporter>();
builder.Services.AddHostedService<ImportWorker>();
builder.Services.AddHostedService<ImportScheduler>();

var app = builder.Build();

// ---- Middleware: the order every request passes through -----------------------------------

app.UseMiddleware<CorrelationIdMiddleware>();
app.UseExceptionHandler();
app.UseStatusCodePages();

// The React frontend, when it has been built into wwwroot (the Docker image does that).
// Locally the Vite dev server serves it instead, so there is nothing to serve here.
var hasFrontend = app.Environment.WebRootFileProvider.GetFileInfo("index.html").Exists;
if (hasFrontend)
{
    app.UseDefaultFiles();
    app.UseStaticFiles();
}

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();
app.MapHealthChecks("/health").AllowAnonymous();
if (app.Configuration.GetValue<bool>("Docs:Enabled"))
{
    app.MapOpenApi().AllowAnonymous();
    app.MapScalarApiReference("/docs", o =>
        {
            o.WithTitle("Company Registry API").DisableAgent().DisableTelemetry();
            o.ShowDeveloperTools = DeveloperToolsVisibility.Never;
            // Hide Scalar's "Generate SDK" promotions; they are not part of this API.
            o.CustomCss = ".explore-scalar, .generate-sdk-button, .selected-client-action.scalar-button-gradient { display: none !important; }";
        })
        .AllowAnonymous();
}

if (hasFrontend)
{
    // Paths like /companies/912855414 are React routes, so they get the app's index.html.
    // Unknown /api and /connect paths are left alone and still answer 404.
    app.MapFallbackToFile("{*path:regex(^(?!api/|connect/).*$)}", "index.html").AllowAnonymous();
}

if (app.Configuration.GetValue<bool>("Database:MigrateOnStartup"))
{
    await using var scope = app.Services.CreateAsyncScope();
    await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.MigrateAsync();
}

app.Run();
```

### src/CompanyRegistry.Api/Domain/OrganisationNumber.cs

- Pure business rule, no framework: 9 digits, weights 3 2 7 6 5 4 3 2, MOD11 check digit (remainder 10 = never valid).
- `static class`: no state, just functions. Easy to unit test.

```csharp
namespace CompanyRegistry.Api.Domain;

/// <summary>
/// Norwegian organisation number (organisasjonsnummer): nine digits, the last one a MOD11 check digit.
/// </summary>
public static class OrganisationNumber
{
    private static readonly int[] Weights = [3, 2, 7, 6, 5, 4, 3, 2];

    public static bool IsValid(string? value)
    {
        if (value is null || value.Length != 9 || !value.All(char.IsAsciiDigit))
        {
            return false;
        }

        var sum = 0;
        for (var i = 0; i < Weights.Length; i++)
        {
            sum += (value[i] - '0') * Weights[i];
        }

        var control = 11 - (sum % 11);
        if (control == 11)
        {
            control = 0;
        }

        // A remainder that gives 10 is never issued, so such a number is always invalid.
        return control != 10 && control == value[8] - '0';
    }
}
```

### src/CompanyRegistry.Api/Data/Company.cs

- An **entity**: one row in `companies`. Plain properties; EF Core maps them to columns.
- `required`: the compiler forces it to be set when creating the object.

```csharp
namespace CompanyRegistry.Api.Data;

public class Company
{
    public required string OrganisationNumber { get; set; }
    public string Name { get; set; } = "";
    public string OrganisationFormCode { get; set; } = "";
    public string? StreetAddress { get; set; }
    public string? PostalCode { get; set; }
    public string? PostalPlace { get; set; }
    public string? MunicipalityNumber { get; set; }
    public string? Municipality { get; set; }
    public DateOnly? RegistrationDate { get; set; }

    public DateTimeOffset FirstImportedAt { get; set; }
    public DateTimeOffset LastChangedAt { get; set; }
}
```

### src/CompanyRegistry.Api/Data/ImportRun.cs

- The import log: one row per run (status, who, correlation id, counts, error).
- `enum ImportStatus` is stored as text (`HasConversion<string>()` in the DbContext), readable in psql.
- `IsActive` is a computed property, not a column (`Ignore`).

```csharp
namespace CompanyRegistry.Api.Data;

public enum ImportStatus
{
    Queued,
    Running,
    Succeeded,
    Failed,
    Cancelled,
    Abandoned,
}

/// <summary>One row per import: when it ran, who asked for it, and what happened to the rows.</summary>
public class ImportRun
{
    public long Id { get; set; }
    public ImportStatus Status { get; set; }
    public required string Trigger { get; set; }
    public string? RequestedBy { get; set; }
    public string? CorrelationId { get; set; }

    public DateTimeOffset QueuedAt { get; set; }
    public DateTimeOffset? StartedAt { get; set; }
    public DateTimeOffset? FinishedAt { get; set; }

    public int RowsRead { get; set; }
    public int RowsInserted { get; set; }
    public int RowsUpdated { get; set; }
    public int RowsUnchanged { get; set; }
    public int RowsRejected { get; set; }
    public string? Error { get; set; }

    public bool IsActive => Status is ImportStatus.Queued or ImportStatus.Running;
}
```

### src/CompanyRegistry.Api/Data/AppDbContext.cs

- **`DbContext`** = a session with the database + unit of work. **`DbSet<T>`** = a table you can query with LINQ.
- `OnModelCreating` is the mapping: keys, lengths, indexes, **short column names** (`HasColumnName("orgnr")`)
  while the C# names stay descriptive.
- Snake_case tables/columns come from `UseSnakeCaseNamingConvention()` in Program.cs.
- Change this file or an entity → `dotnet ef migrations add <Name>`.

```csharp
using Microsoft.EntityFrameworkCore;

namespace CompanyRegistry.Api.Data;

public class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<Company> Companies => Set<Company>();
    public DbSet<ImportRun> ImportRuns => Set<ImportRun>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Company>(company =>
        {
            company.HasKey(c => c.OrganisationNumber);
            // Short column names in the database; the C# names stay descriptive.
            company.Property(c => c.OrganisationNumber).HasColumnName("orgnr").HasMaxLength(9).IsFixedLength();
            company.Property(c => c.Name).HasMaxLength(500);
            company.Property(c => c.OrganisationFormCode).HasColumnName("form_code").HasMaxLength(10);
            company.Property(c => c.StreetAddress).HasMaxLength(500);
            company.Property(c => c.PostalCode).HasMaxLength(10);
            company.Property(c => c.PostalPlace).HasMaxLength(100);
            company.Property(c => c.MunicipalityNumber).HasMaxLength(4);
            company.Property(c => c.Municipality).HasMaxLength(100);

            company.HasIndex(c => c.OrganisationFormCode);
            company.HasIndex(c => c.MunicipalityNumber);
            company.HasIndex(c => c.PostalCode);
        });

        modelBuilder.Entity<ImportRun>(run =>
        {
            run.Property(r => r.Status).HasConversion<string>().HasMaxLength(20);
            run.Property(r => r.Trigger).HasMaxLength(20);
            run.Property(r => r.RequestedBy).HasMaxLength(100);
            run.Property(r => r.CorrelationId).HasMaxLength(64);
            run.Ignore(r => r.IsActive);
            run.HasIndex(r => r.QueuedAt);
        });
    }
}
```

### src/CompanyRegistry.Api/Contracts/CompanyContracts.cs

- **DTOs** = the API's shape (the public contract), separate from the entities (the database's shape).
- `record`: data class with value equality and short syntax.
- `FromEntity` is an `Expression`, so EF Core turns it into a SQL `SELECT` of only those columns.
- `CompanySearchQuery` has validation attributes; `[ApiController]` returns 400 automatically when they fail.

```csharp
using System.ComponentModel.DataAnnotations;
using System.Linq.Expressions;
using CompanyRegistry.Api.Data;

namespace CompanyRegistry.Api.Contracts;

public sealed record CompanyDto(
    string OrganisationNumber,
    string Name,
    string OrganisationFormCode,
    AddressDto Address,
    DateOnly? RegistrationDate,
    DateTimeOffset LastChangedAt)
{
    /// <summary>Used inside LINQ queries, so EF Core selects only these columns instead of loading the whole entity.</summary>
    public static readonly Expression<Func<Company, CompanyDto>> FromEntity = c => new CompanyDto(
        c.OrganisationNumber,
        c.Name,
        c.OrganisationFormCode,
        new AddressDto(c.StreetAddress, c.PostalCode, c.PostalPlace, c.MunicipalityNumber, c.Municipality),
        c.RegistrationDate,
        c.LastChangedAt);
}

public sealed record AddressDto(
    string? Street,
    string? PostalCode,
    string? PostalPlace,
    string? MunicipalityNumber,
    string? Municipality);

public sealed record CompanySearchQuery
{
    /// <summary>Part of the company name, case-insensitive.</summary>
    [StringLength(100)]
    public string? Name { get; init; }

    /// <summary>Organisation form code, e.g. AS, ENK, NUF.</summary>
    [StringLength(10)]
    public string? OrganisationForm { get; init; }

    [StringLength(4)]
    public string? PostalCode { get; init; }

    public DateOnly? RegisteredFrom { get; init; }
    public DateOnly? RegisteredTo { get; init; }

    [Range(1, 10_000)]
    public int Page { get; init; } = 1;

    [Range(1, 100)]
    public int PageSize { get; init; } = 20;
}

public sealed record PagedResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount);
```

### src/CompanyRegistry.Api/Contracts/ImportContracts.cs

The import run as the API shows it.

```csharp
using CompanyRegistry.Api.Data;

namespace CompanyRegistry.Api.Contracts;

public sealed record ImportRunDto(
    long Id,
    string Status,
    string Trigger,
    string? RequestedBy,
    string? CorrelationId,
    DateTimeOffset QueuedAt,
    DateTimeOffset? StartedAt,
    DateTimeOffset? FinishedAt,
    int RowsRead,
    int RowsInserted,
    int RowsUpdated,
    int RowsUnchanged,
    int RowsRejected,
    string? Error)
{
    public static ImportRunDto From(ImportRun r) => new(
        r.Id, r.Status.ToString(), r.Trigger, r.RequestedBy, r.CorrelationId,
        r.QueuedAt, r.StartedAt, r.FinishedAt,
        r.RowsRead, r.RowsInserted, r.RowsUpdated, r.RowsUnchanged, r.RowsRejected, r.Error);
}
```

### src/CompanyRegistry.Api/Controllers/CompaniesController.cs

- **Attributes:** `[ApiController]` (API behaviour, auto 400), `[Route]` (URL prefix), `[Authorize(Policy=…)]` (needs `companies.read`).
- **Primary constructor** `(AppDbContext db)`: DI passes the DbContext in.
- **`IQueryable`**: each filter adds a `Where`; nothing runs until `CountAsync` / `ToListAsync` (2 SQL statements).
- `AsNoTracking()` for reads. `EF.Functions.ILike` with escaped `%`/`_`.
- 400 (bad MOD11) vs 404 (valid but unknown): `Problem(...)` returns RFC 9457 problem details.

```csharp
using CompanyRegistry.Api.Auth;
using CompanyRegistry.Api.Contracts;
using CompanyRegistry.Api.Data;
using CompanyRegistry.Api.Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CompanyRegistry.Api.Controllers;

[ApiController]
[Route("api/companies")]
[Authorize(Policy = Scopes.CompaniesRead)]
public sealed class CompaniesController(AppDbContext db) : ControllerBase
{
    /// <summary>Search companies. All filters are optional and combine with AND.</summary>
    [HttpGet]
    public async Task<ActionResult<PagedResult<CompanyDto>>> Search([FromQuery] CompanySearchQuery query, CancellationToken ct)
    {
        // Nothing runs against the database while the query is being built up;
        // CountAsync and ToListAsync below are what send SQL.
        IQueryable<Company> companies = db.Companies.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(query.Name))
        {
            companies = companies.Where(c => EF.Functions.ILike(c.Name, $"%{EscapeLike(query.Name.Trim())}%", @"\"));
        }
        if (!string.IsNullOrWhiteSpace(query.OrganisationForm))
        {
            var form = query.OrganisationForm.Trim().ToUpperInvariant();
            companies = companies.Where(c => c.OrganisationFormCode == form);
        }
        if (!string.IsNullOrWhiteSpace(query.PostalCode))
        {
            companies = companies.Where(c => c.PostalCode == query.PostalCode);
        }
        if (query.RegisteredFrom is { } from)
        {
            companies = companies.Where(c => c.RegistrationDate >= from);
        }
        if (query.RegisteredTo is { } to)
        {
            companies = companies.Where(c => c.RegistrationDate <= to);
        }

        var total = await companies.CountAsync(ct);
        var items = await companies
            .OrderBy(c => c.Name)
            .ThenBy(c => c.OrganisationNumber)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(CompanyDto.FromEntity)
            .ToListAsync(ct);

        return new PagedResult<CompanyDto>(items, query.Page, query.PageSize, total);
    }

    /// <summary>Get one company by its nine-digit organisation number.</summary>
    [HttpGet("{organisationNumber}")]
    public async Task<ActionResult<CompanyDto>> GetByOrganisationNumber(string organisationNumber, CancellationToken ct)
    {
        if (!OrganisationNumber.IsValid(organisationNumber))
        {
            return Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Invalid organisation number",
                detail: "An organisation number is nine digits with a valid MOD11 check digit.");
        }

        var company = await db.Companies
            .AsNoTracking()
            .Where(c => c.OrganisationNumber == organisationNumber)
            .Select(CompanyDto.FromEntity)
            .FirstOrDefaultAsync(ct);

        return company is null ? NotFound() : company;
    }

    private static string EscapeLike(string value) =>
        value.Replace(@"\", @"\\").Replace("%", @"\%").Replace("_", @"\_");
}
```

### src/CompanyRegistry.Api/Controllers/ImportsController.cs

- `POST` returns **202 Accepted** at once with a `Location` to poll; the work happens in the background.
- **409 Conflict** if an import is already queued or running.
- Needs `companies.import`. The caller's `client_id` claim is saved as `RequestedBy`.

```csharp
using CompanyRegistry.Api.Auth;
using CompanyRegistry.Api.Contracts;
using CompanyRegistry.Api.Data;
using CompanyRegistry.Api.Import;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CompanyRegistry.Api.Controllers;

[ApiController]
[Route("api/imports")]
[Authorize(Policy = Scopes.CompaniesImport)]
public sealed class ImportsController(AppDbContext db, ImportRunService importRuns) : ControllerBase
{
    /// <summary>Queue an import from Brreg. Returns 202 at once; poll the Location to follow progress.</summary>
    [HttpPost]
    [ProducesResponseType<ImportRunDto>(StatusCodes.Status202Accepted)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Start(CancellationToken ct)
    {
        var clientId = User.FindFirst("client_id")?.Value;
        var result = await importRuns.QueueAsync("api", clientId, HttpContext.TraceIdentifier, ct);

        if (result.AlreadyActive)
        {
            return Problem(
                statusCode: StatusCodes.Status409Conflict,
                title: "An import is already in progress",
                detail: $"Import run {result.Run.Id} is {result.Run.Status}.",
                instance: Url.Action(nameof(Get), new { id = result.Run.Id }));
        }

        return AcceptedAtAction(nameof(Get), new { id = result.Run.Id }, ImportRunDto.From(result.Run));
    }

    /// <summary>The most recent import runs, newest first.</summary>
    [HttpGet]
    public async Task<IReadOnlyList<ImportRunDto>> List(CancellationToken ct)
    {
        var runs = await db.ImportRuns.AsNoTracking().OrderByDescending(r => r.Id).Take(20).ToListAsync(ct);
        return runs.Select(ImportRunDto.From).ToList();
    }

    [HttpGet("{id:long}")]
    public async Task<ActionResult<ImportRunDto>> Get(long id, CancellationToken ct)
    {
        var run = await db.ImportRuns.AsNoTracking().FirstOrDefaultAsync(r => r.Id == id, ct);
        return run is null ? NotFound() : ImportRunDto.From(run);
    }
}
```

### src/CompanyRegistry.Api/Controllers/TokenController.cs

- OAuth 2.0 **client credentials** token endpoint, a local stand-in for Maskinporten.
- `[AllowAnonymous]` (you can't need a token to get a token). Off unless `Auth:EnableDevTokenEndpoint` is true.
- Form fields with OAuth names (`grant_type`, `client_id`, …), errors `unsupported_grant_type` / `invalid_client`.

```csharp
using CompanyRegistry.Api.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace CompanyRegistry.Api.Controllers;

public sealed record TokenRequest
{
    [FromForm(Name = "grant_type")] public string? GrantType { get; init; }
    [FromForm(Name = "client_id")] public string? ClientId { get; init; }
    [FromForm(Name = "client_secret")] public string? ClientSecret { get; init; }
    [FromForm(Name = "scope")] public string? Scope { get; init; }
}

/// <summary>
/// OAuth 2.0 client-credentials token endpoint, for local development only.
/// Off unless Auth:EnableDevTokenEndpoint is true.
/// </summary>
[ApiController]
[Route("connect/token")]
[AllowAnonymous]
public sealed class TokenController(DevTokenIssuer issuer, IOptions<AuthOptions> options) : ControllerBase
{
    [HttpPost]
    [Consumes("application/x-www-form-urlencoded")]
    public IActionResult Token([FromForm] TokenRequest request)
    {
        if (!options.Value.EnableDevTokenEndpoint)
        {
            return NotFound();
        }
        if (request.GrantType != "client_credentials")
        {
            return BadRequest(new { error = "unsupported_grant_type" });
        }

        var token = issuer.Issue(request.ClientId ?? "", request.ClientSecret ?? "", request.Scope);
        if (token is null)
        {
            return Unauthorized(new { error = "invalid_client" });
        }

        return Ok(new
        {
            access_token = token.AccessToken,
            token_type = "Bearer",
            expires_in = token.ExpiresIn,
            scope = token.Scope,
        });
    }
}
```

### src/CompanyRegistry.Api/Auth/Scopes.cs

- Scope names, also used as policy names.
- `RequireScope` is an **extension method** (`this AuthorizationPolicyBuilder`): it reads the space-separated
  `scope` claim, as Maskinporten tokens have it.

```csharp
using Microsoft.AspNetCore.Authorization;

namespace CompanyRegistry.Api.Auth;

/// <summary>
/// The scopes this API understands. Each one is also the name of an authorization policy,
/// the same way an API protected by Maskinporten checks the scope claim in the access token.
/// </summary>
public static class Scopes
{
    public const string CompaniesRead = "companies.read";
    public const string CompaniesImport = "companies.import";

    public const string ClaimType = "scope";

    /// <summary>Requires the token's space-separated "scope" claim to contain <paramref name="scope"/>.</summary>
    public static AuthorizationPolicyBuilder RequireScope(this AuthorizationPolicyBuilder policy, string scope) =>
        policy.RequireAssertion(context => context.User
            .FindAll(ClaimType)
            .SelectMany(claim => claim.Value.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            .Contains(scope, StringComparer.Ordinal));
}
```

### src/CompanyRegistry.Api/Auth/AuthOptions.cs

The `Auth` config section as a typed class, with validation (`[Required]`, `[MinLength(32)]`).

```csharp
using System.ComponentModel.DataAnnotations;

namespace CompanyRegistry.Api.Auth;

public sealed class AuthOptions
{
    public const string Section = "Auth";

    [Required]
    public string Issuer { get; set; } = "";

    [Required]
    public string Audience { get; set; } = "";

    /// <summary>HMAC key for the development token issuer. At least 32 characters.</summary>
    [Required, MinLength(32)]
    public string SigningKey { get; set; } = "";

    public bool EnableDevTokenEndpoint { get; set; }

    public List<ClientOptions> Clients { get; set; } = [];

    public TimeSpan TokenLifetime { get; set; } = TimeSpan.FromHours(1);
}

public sealed class ClientOptions
{
    public string ClientId { get; set; } = "";
    public string ClientSecret { get; set; } = "";
    public List<string> Scopes { get; set; } = [];
}
```

### src/CompanyRegistry.Api/Auth/DevTokenIssuer.cs

- Checks client id + secret (`FixedTimeEquals`: constant-time compare, no timing attacks).
- A client gets at most its configured scopes, never more.
- Signs a short-lived JWT (HMAC). With Maskinporten this class disappears; the API validates Maskinporten's
  RS256 tokens via its public keys instead. Policies and controllers stay the same.

```csharp
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace CompanyRegistry.Api.Auth;

public sealed record IssuedToken(string AccessToken, int ExpiresIn, string Scope);

/// <summary>
/// Stand-in for Maskinporten in local development: it checks a client id and secret against
/// configuration and signs a short-lived JWT carrying the client's scopes. In production this
/// class goes away and the API trusts tokens signed by Maskinporten instead.
/// </summary>
public sealed class DevTokenIssuer(IOptions<AuthOptions> options)
{
    public IssuedToken? Issue(string clientId, string clientSecret, string? requestedScope)
    {
        var o = options.Value;
        var client = o.Clients.FirstOrDefault(c => c.ClientId == clientId);
        if (client is null || !SecretsMatch(client.ClientSecret, clientSecret))
        {
            return null;
        }

        // A client may ask for fewer scopes than it is allowed, never more.
        var scopes = requestedScope is null
            ? client.Scopes
            : requestedScope.Split(' ', StringSplitOptions.RemoveEmptyEntries).Intersect(client.Scopes).ToList();
        var scope = string.Join(' ', scopes);

        var token = new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
        {
            Issuer = o.Issuer,
            Audience = o.Audience,
            Expires = DateTime.UtcNow.Add(o.TokenLifetime),
            Subject = new ClaimsIdentity(
            [
                new Claim("sub", client.ClientId),
                new Claim("client_id", client.ClientId),
                new Claim(Scopes.ClaimType, scope),
                new Claim("jti", Guid.NewGuid().ToString("N")),
            ]),
            SigningCredentials = new SigningCredentials(SigningKey(o), SecurityAlgorithms.HmacSha256),
        });

        return new IssuedToken(token, (int)o.TokenLifetime.TotalSeconds, scope);
    }

    public static SymmetricSecurityKey SigningKey(AuthOptions o) => new(Encoding.UTF8.GetBytes(o.SigningKey));

    private static bool SecretsMatch(string expected, string actual) =>
        CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(expected), Encoding.UTF8.GetBytes(actual));
}
```

### src/CompanyRegistry.Api/Infrastructure/CorrelationIdMiddleware.cs

- Custom **middleware**: a class with `InvokeAsync(HttpContext)` that calls `next(context)`.
- Takes a safe `X-Correlation-ID` from the caller or generates one; returns it in the response header.
- `BeginScope`: every log line during the request carries `CorrelationId`.
- Logs method, path, status and duration; `/health` at Debug so platform polling doesn't flood the log.
- `[GeneratedRegex]`: the regex is compiled at build time (source generator).

```csharp
using System.Diagnostics;
using System.Text.RegularExpressions;

namespace CompanyRegistry.Api.Infrastructure;

/// <summary>
/// Gives every request a correlation id: the caller's X-Correlation-ID if it is sane, otherwise a new one.
/// The id is echoed back in the response header and attached to every log line written during the request.
/// </summary>
public sealed partial class CorrelationIdMiddleware(RequestDelegate next, ILogger<CorrelationIdMiddleware> logger)
{
    public const string HeaderName = "X-Correlation-ID";

    public async Task InvokeAsync(HttpContext context)
    {
        var incoming = context.Request.Headers[HeaderName].ToString();
        var correlationId = SafeId().IsMatch(incoming) ? incoming : Guid.NewGuid().ToString("N");

        context.TraceIdentifier = correlationId;
        context.Response.Headers[HeaderName] = correlationId;

        using var scope = logger.BeginScope("CorrelationId {CorrelationId}", correlationId);
        var started = Stopwatch.GetTimestamp();
        try
        {
            await next(context);
        }
        finally
        {
            // The hosting platform polls /health every few seconds; keep those out of the normal log.
            var level = context.Request.Path.StartsWithSegments("/health") ? LogLevel.Debug : LogLevel.Information;
            logger.Log(
                level,
                "HTTP {Method} {Path} responded {StatusCode} in {ElapsedMs:0.0} ms",
                context.Request.Method,
                context.Request.Path.Value,
                context.Response.StatusCode,
                Stopwatch.GetElapsedTime(started).TotalMilliseconds);
        }
    }

    // Only accept ids that are safe to write into logs and headers.
    [GeneratedRegex("^[A-Za-z0-9._-]{1,64}$")]
    private static partial Regex SafeId();
}
```

### src/CompanyRegistry.Api/Infrastructure/BearerSecuritySchemeTransformer.cs

Adds the "Bearer token" field to the OpenAPI document, so the docs page can send authenticated requests.

```csharp
using Microsoft.AspNetCore.OpenApi;
using Microsoft.OpenApi;

namespace CompanyRegistry.Api.Infrastructure;

/// <summary>Adds a Bearer token field to the OpenAPI document, so the API reference page can send authenticated requests.</summary>
internal sealed class BearerSecuritySchemeTransformer : IOpenApiDocumentTransformer
{
    public Task TransformAsync(OpenApiDocument document, OpenApiDocumentTransformerContext context, CancellationToken ct)
    {
        document.Components ??= new OpenApiComponents();
        document.Components.SecuritySchemes ??= new Dictionary<string, IOpenApiSecurityScheme>();
        document.Components.SecuritySchemes["Bearer"] = new OpenApiSecurityScheme
        {
            Type = SecuritySchemeType.Http,
            Scheme = "bearer",
            BearerFormat = "JWT",
            Description = "Get a token from POST /connect/token with a client id and secret from appsettings.Development.json.",
        };

        document.Security ??= [];
        document.Security.Add(new OpenApiSecurityRequirement
        {
            [new OpenApiSecuritySchemeReference("Bearer", document)] = [],
        });
        return Task.CompletedTask;
    }
}
```

### src/CompanyRegistry.Api/Import/ImportOptions.cs

- **Options pattern**: the `Import` section bound to a class, validated at startup.
- Lists bind from JSON arrays: `"MunicipalityNumbers": ["3901", "3905"]`.
- `MaxRows` 10 000: the Brreg search API refuses to page further.

```csharp
using System.ComponentModel.DataAnnotations;

namespace CompanyRegistry.Api.Import;

public sealed class ImportOptions
{
    public const string Section = "Import";

    [Required, Url]
    public string BaseUrl { get; set; } = "https://data.brreg.no/";

    /// <summary>Kommunenummer filter, e.g. 3901 (Horten) and 3905 (Tønsberg). Empty imports every municipality.</summary>
    public List<string> MunicipalityNumbers { get; set; } = [];

    /// <summary>Organisasjonsform filter, e.g. AS and ASA. Empty imports every form.</summary>
    public List<string> OrganisationForms { get; set; } = [];

    [Range(1, 1000)]
    public int PageSize { get; set; } = 500;

    /// <summary>The Brreg search API refuses to page past 10 000 rows; the bulk download is the way past that.</summary>
    [Range(1, 10_000)]
    public int MaxRows { get; set; } = 10_000;

    /// <summary>How often the scheduler queues an import. Null turns the schedule off.</summary>
    public TimeSpan? ScheduleInterval { get; set; }
}
```

### src/CompanyRegistry.Api/Import/BrregModels.cs

- Records for the parts of Brreg's JSON we read. Names match the API (case-insensitive), so almost no attributes.
- Everything nullable: external data is untrusted.
- We don't read e-mail or phone (privacy by design).

```csharp
using System.Text.Json.Serialization;

namespace CompanyRegistry.Api.Import;

// Shapes of the Enhetsregisteret JSON we read. Property names match the API so no mapping attributes are needed.

public sealed record BrregPage(
    [property: JsonPropertyName("_embedded")] BrregEmbedded? Embedded,
    BrregPageInfo Page);

public sealed record BrregEmbedded(IReadOnlyList<BrregEnhet> Enheter);

public sealed record BrregPageInfo(int Size, int TotalElements, int TotalPages, int Number);

public sealed record BrregEnhet(
    string? Organisasjonsnummer,
    string? Navn,
    BrregKode? Organisasjonsform,
    BrregAdresse? Forretningsadresse,
    DateOnly? RegistreringsdatoEnhetsregisteret);

public sealed record BrregKode(string? Kode);

public sealed record BrregAdresse(
    IReadOnlyList<string>? Adresse,
    string? Postnummer,
    string? Poststed,
    string? Kommunenummer,
    string? Kommune);
```

### src/CompanyRegistry.Api/Import/BrregClient.cs

- **Typed HttpClient**: the `HttpClient` comes from `IHttpClientFactory` with base address and retries
  (configured in Program.cs). Never `new HttpClient()` per call (socket exhaustion, stale DNS).
- `GetFromJsonAsync<T>`: GET + deserialize in one call.

```csharp
using Microsoft.Extensions.Options;

namespace CompanyRegistry.Api.Import;

/// <summary>
/// Typed HTTP client for Enhetsregisteret. The HttpClient is created and configured by IHttpClientFactory
/// (base address, retries, timeouts) in Program.cs; this class only knows how to ask for a page.
/// </summary>
public sealed class BrregClient(HttpClient http, IOptions<ImportOptions> options)
{
    public async Task<BrregPage> GetPageAsync(int page, CancellationToken ct)
    {
        var o = options.Value;
        var query = new Dictionary<string, string?>
        {
            ["page"] = page.ToString(),
            ["size"] = o.PageSize.ToString(),
            // Brreg takes several values as a comma-separated list.
            ["kommunenummer"] = string.Join(',', o.MunicipalityNumbers),
            ["organisasjonsform"] = string.Join(',', o.OrganisationForms),
        };
        var url = "enhetsregisteret/api/enheter?" + string.Join('&', query
            .Where(kv => !string.IsNullOrEmpty(kv.Value))
            .Select(kv => $"{kv.Key}={Uri.EscapeDataString(kv.Value!)}"));

        return await http.GetFromJsonAsync<BrregPage>(url, ct)
            ?? throw new InvalidOperationException($"Brreg returned an empty body for page {page}.");
    }
}
```

### src/CompanyRegistry.Api/Import/ImportQueue.cs

- In-process queue: `Channel<long>` (thread-safe producer/consumer). Singleton, so API and worker share it.
- Like Redis `LPUSH`/`BRPOP`, but in memory: lost on restart, one process only.

```csharp
using System.Threading.Channels;

namespace CompanyRegistry.Api.Import;

/// <summary>
/// In-process queue between whoever asks for an import (the API, the scheduler) and the worker that runs it.
/// Registered as a singleton, so producers and the consumer share the same channel.
/// </summary>
public sealed class ImportQueue
{
    private readonly Channel<long> _channel = Channel.CreateUnbounded<long>();

    public ValueTask EnqueueAsync(long importRunId, CancellationToken ct) => _channel.Writer.WriteAsync(importRunId, ct);

    public IAsyncEnumerable<long> ReadAllAsync(CancellationToken ct) => _channel.Reader.ReadAllAsync(ct);
}
```

### src/CompanyRegistry.Api/Import/ImportRunService.cs

Creates the `import_runs` row and enqueues it; refuses if one is already active.

```csharp
using CompanyRegistry.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace CompanyRegistry.Api.Import;

public sealed record QueueResult(ImportRun Run, bool AlreadyActive);

/// <summary>Creates an import_runs row and puts it on the queue. Only one import may be queued or running at a time.</summary>
public sealed class ImportRunService(AppDbContext db, ImportQueue queue)
{
    public async Task<QueueResult> QueueAsync(string trigger, string? requestedBy, string? correlationId, CancellationToken ct)
    {
        var active = await db.ImportRuns
            .Where(r => r.Status == ImportStatus.Queued || r.Status == ImportStatus.Running)
            .OrderByDescending(r => r.Id)
            .FirstOrDefaultAsync(ct);
        if (active is not null)
        {
            return new QueueResult(active, AlreadyActive: true);
        }

        var run = new ImportRun
        {
            Status = ImportStatus.Queued,
            Trigger = trigger,
            RequestedBy = requestedBy,
            CorrelationId = correlationId,
            QueuedAt = DateTimeOffset.UtcNow,
        };
        db.ImportRuns.Add(run);
        await db.SaveChangesAsync(ct);

        await queue.EnqueueAsync(run.Id, ct);
        return new QueueResult(run, AlreadyActive: false);
    }
}
```

### src/CompanyRegistry.Api/Import/CompanyImporter.cs

**The import, page by page:**
1. Fetch a page from Brreg (`await`, with cancellation).
2. Validate each unit (MOD11, name, form): rejected rows are counted and logged with the reason.
3. Load the existing companies of the page in **one query** (`Contains` → SQL `IN`).
4. Upsert: insert new ones; for existing ones, set the values and let **change tracking** decide
   (`EntityState.Modified` only if something really changed; unchanged = no SQL).
5. `SaveChangesAsync` per page = one transaction per page; `ChangeTracker.Clear()` keeps memory flat.

Errors: cancellation → `Cancelled`, anything else → `Failed` with the message; the run row always gets
`FinishedAt`. Log scope carries `ImportRunId` + `CorrelationId` from the HTTP request that started it.

```csharp
using CompanyRegistry.Api.Data;
using CompanyRegistry.Api.Domain;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

namespace CompanyRegistry.Api.Import;

/// <summary>
/// Runs one import: pages through Brreg, validates each unit, and upserts into companies.
/// Each page is committed on its own, so a failure halfway keeps the pages before it,
/// and running the import again is safe because rows are matched on organisation number.
/// </summary>
public sealed class CompanyImporter(
    AppDbContext db,
    BrregClient brreg,
    IOptions<ImportOptions> options,
    ILogger<CompanyImporter> logger)
{
    public async Task RunAsync(long importRunId, CancellationToken ct)
    {
        var run = await db.ImportRuns.FindAsync([importRunId], ct)
            ?? throw new InvalidOperationException($"Import run {importRunId} does not exist.");

        using var scope = logger.BeginScope("ImportRun {ImportRunId}, CorrelationId {CorrelationId}", run.Id, run.CorrelationId);

        run.Status = ImportStatus.Running;
        run.StartedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(ct);
        logger.LogInformation("Import started, triggered by {Trigger}", run.Trigger);

        try
        {
            await ImportAllPagesAsync(run, ct);
            run.Status = ImportStatus.Succeeded;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            run.Status = ImportStatus.Cancelled;
            logger.LogWarning("Import cancelled because the application is stopping");
        }
        catch (Exception ex)
        {
            run.Status = ImportStatus.Failed;
            run.Error = ex.Message;
            logger.LogError(ex, "Import failed");
        }

        // Throw away any half-applied page, then record how the run ended.
        db.ChangeTracker.Clear();
        db.ImportRuns.Attach(run).State = EntityState.Modified;
        run.FinishedAt = DateTimeOffset.UtcNow;
        await db.SaveChangesAsync(CancellationToken.None);

        logger.LogInformation(
            "Import finished with status {Status}: read {RowsRead}, inserted {RowsInserted}, updated {RowsUpdated}, unchanged {RowsUnchanged}, rejected {RowsRejected}",
            run.Status, run.RowsRead, run.RowsInserted, run.RowsUpdated, run.RowsUnchanged, run.RowsRejected);
    }

    private async Task ImportAllPagesAsync(ImportRun run, CancellationToken ct)
    {
        var o = options.Value;
        for (var page = 0; ; page++)
        {
            if ((page + 1) * o.PageSize > o.MaxRows)
            {
                logger.LogWarning("Stopped at {MaxRows} rows, the most the Brreg search API will page through", o.MaxRows);
                return;
            }

            var result = await brreg.GetPageAsync(page, ct);
            var units = result.Embedded?.Enheter ?? [];

            await UpsertPageAsync(units, run, ct);
            await db.SaveChangesAsync(ct);
            db.ChangeTracker.Clear();
            db.ImportRuns.Attach(run);

            logger.LogInformation("Imported page {Page} of {TotalPages}", page + 1, result.Page.TotalPages);

            if (page + 1 >= result.Page.TotalPages)
            {
                return;
            }
        }
    }

    private async Task UpsertPageAsync(IReadOnlyList<BrregEnhet> units, ImportRun run, CancellationToken ct)
    {
        run.RowsRead += units.Count;

        var accepted = new List<BrregEnhet>();
        foreach (var unit in units)
        {
            var reason = RejectionReason(unit);
            if (reason is null)
            {
                accepted.Add(unit);
                continue;
            }

            run.RowsRejected++;
            logger.LogWarning("Rejected {OrganisationNumber}: {Reason}", unit.Organisasjonsnummer, reason);
        }

        // One query for the whole page, not one per row.
        var numbers = accepted.Select(u => u.Organisasjonsnummer!).ToList();
        var existing = await db.Companies
            .Where(c => numbers.Contains(c.OrganisationNumber))
            .ToDictionaryAsync(c => c.OrganisationNumber, ct);

        var now = DateTimeOffset.UtcNow;
        foreach (var unit in accepted)
        {
            if (existing.TryGetValue(unit.Organisasjonsnummer!, out var company))
            {
                Apply(unit, company);

                // Change tracking compares against the values loaded from the database,
                // so a company whose data did not change is not written at all.
                if (db.Entry(company).State == EntityState.Modified)
                {
                    company.LastChangedAt = now;
                    run.RowsUpdated++;
                }
                else
                {
                    run.RowsUnchanged++;
                }
            }
            else
            {
                company = new Company { OrganisationNumber = unit.Organisasjonsnummer!, FirstImportedAt = now, LastChangedAt = now };
                Apply(unit, company);
                db.Companies.Add(company);
                existing[company.OrganisationNumber] = company;
                run.RowsInserted++;
            }
        }
    }

    private static string? RejectionReason(BrregEnhet unit) => unit switch
    {
        { Organisasjonsnummer: var n } when !OrganisationNumber.IsValid(n) => "organisation number fails MOD11 check",
        { Navn: null or "" } => "missing name",
        { Organisasjonsform.Kode: null or "" } => "missing organisation form",
        _ => null,
    };

    private static void Apply(BrregEnhet unit, Company company)
    {
        var address = unit.Forretningsadresse;
        company.Name = unit.Navn!;
        company.OrganisationFormCode = unit.Organisasjonsform!.Kode!;
        company.StreetAddress = address?.Adresse is { Count: > 0 } lines ? string.Join(", ", lines) : null;
        company.PostalCode = address?.Postnummer;
        company.PostalPlace = address?.Poststed;
        company.MunicipalityNumber = address?.Kommunenummer;
        company.Municipality = address?.Kommune;
        company.RegistrationDate = unit.RegistreringsdatoEnhetsregisteret;
    }
}
```

### src/CompanyRegistry.Api/Import/ImportWorker.cs

- **`BackgroundService`**: starts with the app, `ExecuteAsync` loops until shutdown. Like a systemd service.
- It is a singleton, so it creates a **new DI scope per run** (`IServiceScopeFactory`) for a fresh,
  short-lived DbContext. Injecting the DbContext directly would be a captive dependency.
- On startup, runs left Queued/Running by a crash become `Abandoned` (`ExecuteUpdateAsync`: one SQL UPDATE).

```csharp
using CompanyRegistry.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace CompanyRegistry.Api.Import;

/// <summary>
/// Long-running background service that takes import runs off the queue and runs them one at a time.
/// It is a singleton, so it creates a fresh DI scope (and with it a fresh DbContext) for every run.
/// </summary>
public sealed class ImportWorker(
    ImportQueue queue,
    IServiceScopeFactory scopeFactory,
    ILogger<ImportWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await AbandonInterruptedRunsAsync(stoppingToken);

        await foreach (var runId in queue.ReadAllAsync(stoppingToken))
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var importer = scope.ServiceProvider.GetRequiredService<CompanyImporter>();
            await importer.RunAsync(runId, stoppingToken);
        }
    }

    /// <summary>The queue lives in memory, so runs that were queued or running when the process stopped will never finish.</summary>
    private async Task AbandonInterruptedRunsAsync(CancellationToken ct)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

        var abandoned = await db.ImportRuns
            .Where(r => r.Status == ImportStatus.Queued || r.Status == ImportStatus.Running)
            .ExecuteUpdateAsync(set => set
                .SetProperty(r => r.Status, ImportStatus.Abandoned)
                .SetProperty(r => r.FinishedAt, DateTimeOffset.UtcNow)
                .SetProperty(r => r.Error, "Interrupted by an application restart"), ct);

        if (abandoned > 0)
        {
            logger.LogWarning("Marked {Count} interrupted import runs as abandoned", abandoned);
        }
    }
}
```

### src/CompanyRegistry.Api/Import/ImportScheduler.cs

- The cron line: `PeriodicTimer` queues an import every `Import:ScheduleInterval`. Off when not set.
- With several instances, each would schedule its own; this project is single-instance by design.

```csharp
using Microsoft.Extensions.Options;

namespace CompanyRegistry.Api.Import;

/// <summary>The cron line of this app: queues an import every Import:ScheduleInterval. Off when the interval is not set.</summary>
public sealed class ImportScheduler(
    IServiceScopeFactory scopeFactory,
    IOptions<ImportOptions> options,
    ILogger<ImportScheduler> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (options.Value.ScheduleInterval is not { } interval)
        {
            logger.LogInformation("Scheduled import is off");
            return;
        }

        logger.LogInformation("Scheduled import every {Interval}", interval);
        using var timer = new PeriodicTimer(interval);
        while (await timer.WaitForNextTickAsync(stoppingToken))
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var runs = scope.ServiceProvider.GetRequiredService<ImportRunService>();
            var result = await runs.QueueAsync("schedule", requestedBy: null, correlationId: Guid.NewGuid().ToString("N"), stoppingToken);
            if (result.AlreadyActive)
            {
                logger.LogInformation("Skipped scheduled import, run {ImportRunId} is still active", result.Run.Id);
            }
        }
    }
}
```

### src/CompanyRegistry.Api/Data/Migrations/ (generated, not hand-written)

Created by `dotnet ef migrations add …`. Review them before committing:
- `InitialCreate`: the two tables and their indexes.
- `ShortenCompanyColumns`: `RenameColumn` (keeps the data, not drop + add), `RenameIndex`, `DropColumn`.
- `AppDbContextModelSnapshot.cs`: EF's picture of the current model, used to compute the next migration.

---

## 5. Tests
### tests/CompanyRegistry.Tests/OrganisationNumberTests.cs

**Unit tests**: one function, no database, no HTTP, milliseconds.
`[Theory]` + `[InlineData]` = the same test with many inputs (PHPUnit data providers). `[Fact]` = one case.

```csharp
using CompanyRegistry.Api.Domain;

namespace CompanyRegistry.Tests;

public class OrganisationNumberTests
{
    [Theory]
    [InlineData("923609016")] // Equinor ASA
    [InlineData("974760673")] // Brønnøysundregistrene
    [InlineData("916627939")]
    [InlineData("912855414")]
    public void Accepts_real_organisation_numbers(string value) =>
        Assert.True(OrganisationNumber.IsValid(value));

    [Theory]
    [InlineData("923609017")]   // wrong check digit
    [InlineData("974760670")]   // wrong check digit
    [InlineData("12345678")]    // too short
    [InlineData("1234567890")]  // too long
    [InlineData("92360901A")]   // not a digit
    [InlineData("923 609 016")] // spaces are not normalised here
    [InlineData("")]
    [InlineData(null)]
    public void Rejects_invalid_values(string? value) =>
        Assert.False(OrganisationNumber.IsValid(value));

    [Fact]
    public void Rejects_numbers_whose_check_digit_would_be_10()
    {
        // Weighted sum 4 * 3 = 12, remainder 1, so 11 - 1 = 10: no valid check digit exists for this prefix.
        Assert.All(Enumerable.Range(0, 10), d => Assert.False(OrganisationNumber.IsValid($"00000040{d}")));
    }
}
```

### tests/CompanyRegistry.Tests/Infrastructure/ApiFactory.cs

- **`WebApplicationFactory<Program>`** boots the real app in memory with a test HTTP client.
- **Testcontainers** starts a throwaway `postgres:17-alpine` in Docker; migrations run on it.
- `UseSetting` overrides config; `ConfigureTestServices` swaps Brreg for a fake HTTP handler.
- `ICollectionFixture`: one container shared by all integration tests.

```csharp
using System.Net.Http.Headers;
using System.Net.Http.Json;
using CompanyRegistry.Api.Import;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;

namespace CompanyRegistry.Tests.Infrastructure;

/// <summary>
/// Runs the real API in memory against a throwaway Postgres in Docker, with Brreg replaced by <see cref="FakeBrreg"/>.
/// Shared by every test in the "api" collection, so the container starts once.
/// </summary>
public sealed class ApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _db = new PostgreSqlBuilder("postgres:17-alpine").Build();

    public FakeBrreg Brreg { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:Companies", _db.GetConnectionString());
        builder.UseSetting("Database:MigrateOnStartup", "true");
        builder.UseSetting("Auth:SigningKey", "test-signing-key-that-is-long-enough-0123456789");
        builder.UseSetting("Auth:EnableDevTokenEndpoint", "true");
        builder.UseSetting("Auth:Clients:0:ClientId", "reader");
        builder.UseSetting("Auth:Clients:0:ClientSecret", "reader-secret");
        builder.UseSetting("Auth:Clients:0:Scopes:0", "companies.read");
        builder.UseSetting("Auth:Clients:1:ClientId", "importer");
        builder.UseSetting("Auth:Clients:1:ClientSecret", "importer-secret");
        builder.UseSetting("Auth:Clients:1:Scopes:0", "companies.read");
        builder.UseSetting("Auth:Clients:1:Scopes:1", "companies.import");

        builder.ConfigureTestServices(services => services
            .AddHttpClient<BrregClient>()
            .ConfigurePrimaryHttpMessageHandler(() => new FakeBrregHandler(Brreg)));
    }

    /// <summary>An HttpClient that sends a bearer token for the given test client.</summary>
    public async Task<HttpClient> CreateClientAsAsync(string clientId)
    {
        var client = CreateClient();
        var response = await client.PostAsync("/connect/token", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "client_credentials",
            ["client_id"] = clientId,
            ["client_secret"] = $"{clientId}-secret",
        }));
        response.EnsureSuccessStatusCode();
        var token = await response.Content.ReadFromJsonAsync<TokenResponse>();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token!.access_token);
        return client;
    }

    public Task InitializeAsync() => _db.StartAsync();

    async Task IAsyncLifetime.DisposeAsync()
    {
        await base.DisposeAsync();
        await _db.DisposeAsync();
    }

    private sealed record TokenResponse(string access_token, string scope);
}

[CollectionDefinition("api")]
public sealed class ApiCollection : ICollectionFixture<ApiFactory>;
```

### tests/CompanyRegistry.Tests/Infrastructure/FakeBrreg.cs

A fake Enhetsregisteret: an `HttpMessageHandler` that returns canned JSON and records the requested URLs.

```csharp
using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Json;

namespace CompanyRegistry.Tests.Infrastructure;

/// <summary>What the fake Enhetsregisteret returns. Tests set <see cref="Units"/> before triggering an import.</summary>
public sealed class FakeBrreg
{
    public IReadOnlyList<object> Units { get; set; } = [];
    public ConcurrentQueue<Uri> Requests { get; } = new();

    public static object Unit(string organisationNumber, string name, string form = "AS") => new
    {
        organisasjonsnummer = organisationNumber,
        navn = name,
        organisasjonsform = new { kode = form, beskrivelse = "Aksjeselskap" },
        forretningsadresse = new
        {
            adresse = new[] { "Storgaten 1" },
            postnummer = "3126",
            poststed = "TØNSBERG",
            kommunenummer = "3905",
            kommune = "TØNSBERG",
        },
        registreringsdatoEnhetsregisteret = "2020-01-15",
    };
}

public sealed class FakeBrregHandler(FakeBrreg brreg) : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        brreg.Requests.Enqueue(request.RequestUri!);
        var units = brreg.Units;
        var body = new
        {
            _embedded = new { enheter = units },
            page = new { size = 500, totalElements = units.Count, totalPages = 1, number = 0 },
        };
        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(body) });
    }
}
```

### tests/CompanyRegistry.Tests/AuthTests.cs

401 without token, 403 with the wrong scope, wrong secret, scope limiting, anonymous health check.

```csharp
using System.Net;
using System.Net.Http.Json;
using CompanyRegistry.Tests.Infrastructure;

namespace CompanyRegistry.Tests;

[Collection("api")]
public class AuthTests(ApiFactory api)
{
    [Fact]
    public async Task Request_without_token_is_401()
    {
        var response = await api.CreateClient().GetAsync("/api/companies");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Token_with_only_read_scope_cannot_start_an_import()
    {
        var reader = await api.CreateClientAsAsync("reader");
        var response = await reader.PostAsync("/api/imports", null);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Wrong_client_secret_gets_no_token()
    {
        var response = await api.CreateClient().PostAsync("/connect/token", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "client_credentials",
            ["client_id"] = "reader",
            ["client_secret"] = "guess",
        }));
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Client_cannot_get_scopes_it_was_not_given()
    {
        var response = await api.CreateClient().PostAsync("/connect/token", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "client_credentials",
            ["client_id"] = "reader",
            ["client_secret"] = "reader-secret",
            ["scope"] = "companies.read companies.import",
        }));
        var body = await response.Content.ReadFromJsonAsync<Dictionary<string, object>>();
        Assert.Equal("companies.read", body!["scope"].ToString());
    }

    [Fact]
    public async Task Health_check_needs_no_token()
    {
        var response = await api.CreateClient().GetAsync("/health");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
```

### tests/CompanyRegistry.Tests/CompaniesApiTests.cs

Seeds rows directly through the DbContext, then tests search, `%` escaping, 400/404, and page-size validation.

```csharp
using System.Net;
using System.Net.Http.Json;
using CompanyRegistry.Api.Contracts;
using CompanyRegistry.Api.Data;
using CompanyRegistry.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CompanyRegistry.Tests;

[Collection("api")]
public class CompaniesApiTests(ApiFactory api) : IAsyncLifetime
{
    public async Task InitializeAsync()
    {
        await using var scope = api.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Companies.Where(c => c.Name.StartsWith("TESTSEED")).ExecuteDeleteAsync();
        db.Companies.AddRange(
            Seed("974760673", "TESTSEED Fjordbåt AS", "AS"),
            Seed("912855414", "TESTSEED Fjellbil AS", "AS"),
            Seed("927386380", "TESTSEED Fjordkafé ENK", "ENK"));
        await db.SaveChangesAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Search_matches_part_of_the_name_case_insensitively_and_filters_by_form()
    {
        var client = await api.CreateClientAsAsync("reader");

        var result = await client.GetFromJsonAsync<PagedResult<CompanyDto>>("/api/companies?name=testseed fjord&organisationForm=as");

        var company = Assert.Single(result!.Items);
        Assert.Equal("974760673", company.OrganisationNumber);
        Assert.Equal(1, result.TotalCount);
    }

    [Fact]
    public async Task Search_treats_percent_as_a_literal_character()
    {
        var client = await api.CreateClientAsAsync("reader");

        var result = await client.GetFromJsonAsync<PagedResult<CompanyDto>>("/api/companies?name=testseed%25");

        Assert.Empty(result!.Items);
    }

    [Fact]
    public async Task Get_by_organisation_number_returns_the_company()
    {
        var client = await api.CreateClientAsAsync("reader");

        var company = await client.GetFromJsonAsync<CompanyDto>("/api/companies/912855414");

        Assert.Equal("TESTSEED Fjellbil AS", company!.Name);
        Assert.Equal("3905", company.Address.MunicipalityNumber);
    }

    [Fact]
    public async Task Get_with_invalid_check_digit_is_400()
    {
        var client = await api.CreateClientAsAsync("reader");
        var response = await client.GetAsync("/api/companies/912855415");
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Get_unknown_but_valid_number_is_404()
    {
        var client = await api.CreateClientAsAsync("reader");
        var response = await client.GetAsync("/api/companies/987654325");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Page_size_above_100_is_rejected()
    {
        var client = await api.CreateClientAsAsync("reader");
        var response = await client.GetAsync("/api/companies?pageSize=1000");
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private static Company Seed(string number, string name, string form) => new()
    {
        OrganisationNumber = number,
        Name = name,
        OrganisationFormCode = form,
        MunicipalityNumber = "3905",
        Municipality = "TØNSBERG",
        FirstImportedAt = DateTimeOffset.UtcNow,
        LastChangedAt = DateTimeOffset.UtcNow,
    };
}
```

### tests/CompanyRegistry.Tests/ImportPipelineTests.cs

End-to-end import: 1 bad row rejected, 2 inserted; the second run updates the renamed company,
leaves the other unchanged, inserts nothing. Polls the run like a real client would.

```csharp
using System.Net;
using System.Net.Http.Json;
using CompanyRegistry.Api.Contracts;
using CompanyRegistry.Tests.Infrastructure;

namespace CompanyRegistry.Tests;

[Collection("api")]
public class ImportPipelineTests(ApiFactory api)
{
    [Fact]
    public async Task Import_rejects_invalid_rows_upserts_the_rest_and_is_safe_to_rerun()
    {
        var client = await api.CreateClientAsAsync("importer");
        client.DefaultRequestHeaders.Add("X-Correlation-ID", "import-test-1");

        api.Brreg.Units =
        [
            FakeBrreg.Unit("923609016", "IMPORTTEST Equinor ASA", "ASA"),
            FakeBrreg.Unit("916627939", "IMPORTTEST Palmera"),
            FakeBrreg.Unit("923609017", "IMPORTTEST Bad check digit"),
        ];

        var first = await RunImportAsync(client);

        Assert.Equal("Succeeded", first.Status);
        Assert.Equal("import-test-1", first.CorrelationId);
        Assert.Equal("importer", first.RequestedBy);
        Assert.Equal((3, 2, 0, 0, 1), (first.RowsRead, first.RowsInserted, first.RowsUpdated, first.RowsUnchanged, first.RowsRejected));
        Assert.Contains(api.Brreg.Requests, uri => uri.Query.Contains("kommunenummer=3901%2C3905&organisasjonsform=AS%2CASA"));

        // Same data plus one renamed company: one update, one untouched row, nothing duplicated.
        api.Brreg.Units =
        [
            FakeBrreg.Unit("923609016", "IMPORTTEST Equinor ASA", "ASA"),
            FakeBrreg.Unit("916627939", "IMPORTTEST Palmera Galleri"),
        ];

        var second = await RunImportAsync(client);

        Assert.Equal((2, 0, 1, 1, 0), (second.RowsRead, second.RowsInserted, second.RowsUpdated, second.RowsUnchanged, second.RowsRejected));

        var renamed = await client.GetFromJsonAsync<CompanyDto>("/api/companies/916627939");
        Assert.Equal("IMPORTTEST Palmera Galleri", renamed!.Name);
        var rejected = await client.GetAsync("/api/companies/923609017");
        Assert.Equal(HttpStatusCode.BadRequest, rejected.StatusCode);
    }

    private static async Task<ImportRunDto> RunImportAsync(HttpClient client)
    {
        var response = await client.PostAsync("/api/imports", null);
        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        var location = response.Headers.Location!;

        // The import runs in a background service; poll the run until it reaches a final state.
        var deadline = DateTime.UtcNow.AddSeconds(30);
        while (DateTime.UtcNow < deadline)
        {
            var run = await client.GetFromJsonAsync<ImportRunDto>(location);
            if (run!.Status is not ("Queued" or "Running"))
            {
                return run;
            }
            await Task.Delay(100);
        }
        throw new TimeoutException("Import did not finish within 30 seconds.");
    }
}
```

---

## 6. Request flow, auth flow, import flow (say these out loud)

1. **Request:** HTTP → CorrelationIdMiddleware → exception handler → authentication (JWT) →
   authorization (policy: scope) → controller → EF Core (LINQ → SQL) → Postgres → DTO → JSON.
2. **Auth:** client id + secret → `/connect/token` → JWT with `scope` → `Authorization: Bearer` →
   signature, issuer, audience, expiry checked → policy checks the scope → 401 / 403 / 200.
   Maskinporten replaces the token issuer, not the policies.
3. **Import:** `POST /api/imports` → `import_runs` row + queue → 202 → worker (new scope) → Brreg page →
   MOD11 → upsert via change tracking → commit per page → counts in `import_runs`, logs under the
   correlation id that started it.
