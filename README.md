# company-registry-api-dotnet

[![CI](https://github.com/muhannadsnan/company-registry-api-dotnet/actions/workflows/ci.yml/badge.svg)](https://github.com/muhannadsnan/company-registry-api-dotnet/actions/workflows/ci.yml)

A small ASP.NET Core Web API over Norwegian company data. It imports companies from
[Enhetsregisteret](https://data.brreg.no/enhetsregisteret/api/dokumentasjon/no/index.html)
(Brønnøysundregistrene) into PostgreSQL, and serves them through a search API protected by
scoped bearer tokens.

.NET 10 · ASP.NET Core (controllers) · Entity Framework Core · PostgreSQL · xUnit · Testcontainers · GitHub Actions

It is deliberately small. The aim is a finished, tidy core where each part can be explained,
not a large feature list.

## Run it

You need Docker and the .NET 10 SDK.

```bash
docker compose up -d                                # Postgres on localhost:5433
dotnet run --project src/CompanyRegistry.Api        # API on http://localhost:5080, migrates the database on start
```

Get a token, start an import, and search:

```bash
# 1. Client-credentials token (development clients are in appsettings.Development.json)
TOKEN=$(curl -s -X POST localhost:5080/connect/token \
  -d grant_type=client_credentials -d client_id=demo-importer -d client_secret=importer-secret | jq -r .access_token)

# 2. Queue an import. Returns 202 Accepted and a Location to poll.
curl -s -X POST localhost:5080/api/imports -H "Authorization: Bearer $TOKEN"
curl -s localhost:5080/api/imports/1 -H "Authorization: Bearer $TOKEN"

# 3. Search
curl -s "localhost:5080/api/companies?name=bil&pageSize=5" -H "Authorization: Bearer $TOKEN"
curl -s localhost:5080/api/companies/912855414 -H "Authorization: Bearer $TOKEN"
```

By default the import fetches all AS and ASA companies in Horten and Tønsberg, about 7,200
companies. That takes roughly five seconds. The filter is set under `Import` in `appsettings.json`.
Brreg's municipality filter matches the postal address as well as the business address, so a
few imported companies have their business address in another municipality.

Interactive API reference: http://localhost:5080/docs (development only; paste the token into the auth field).

Tests: `dotnet test`. Docker must be running, because the integration tests start their own Postgres.

## Endpoints

| Method | Path | Scope | |
|---|---|---|---|
| `GET` | `/api/companies` | `companies.read` | Search. Filters: `name`, `organisationForm`, `postalCode`, `registeredFrom`, `registeredTo`, `page`, `pageSize` |
| `GET` | `/api/companies/{orgnr}` | `companies.read` | One company. 400 if the number fails MOD11, 404 if unknown |
| `POST` | `/api/imports` | `companies.import` | Queue an import. 202, or 409 if one is already running |
| `GET` | `/api/imports`, `/api/imports/{id}` | `companies.import` | Import history with row counts |
| `POST` | `/connect/token` | none | Development token endpoint (client credentials) |
| `GET` | `/health` | none | Checks the database connection |

Errors are returned as RFC 9457 problem details, and each one includes the request's `correlationId`.

## Design choices

### Import

- **Typed `HttpClient` via `IHttpClientFactory`**, with the standard resilience handler
  (retries with backoff, timeouts, circuit breaker). Brreg occasionally answers with a 5xx.
  The import should retry those, not fail on the first one.
- **Queued, not run inside the request.** `POST /api/imports` writes an `import_runs` row
  and puts its id on an in-process `Channel`. A `BackgroundService` takes runs off the queue
  one at a time. The caller gets `202 Accepted` at once and polls the run. Only one import may
  be queued or running at a time.
- **Validated at the boundary.** Every unit is checked before it is stored: the organisation
  number must pass MOD11, and the name and organisation form must be present. Rejected rows are
  counted and logged with the reason, not silently dropped.
- **Upsert, safe to re-run.** Each page loads the existing companies it contains in one query.
  New ones are inserted and existing ones are updated. EF Core's change tracking compares
  against the loaded values, so an unchanged company produces no SQL at all. A second run over
  the same data reports every row as unchanged and writes nothing.
- **One transaction per page.** A failure on page 7 keeps pages 1–6. Running the import again
  picks up where it failed, because the upsert makes repeats harmless.
- **The import log is a table.** `import_runs` records who triggered each run and when, how it
  ended, and rows read, inserted, updated, unchanged and rejected. Runs that were interrupted by
  a restart are marked `Abandoned` on startup, so none stay "Running" forever.
- **Only business fields are stored.** Brreg also returns e-mail addresses and phone numbers;
  the API does not read them.

**Limits.** The Brreg search API will not page past 10,000 rows. The full register (~1.2M
units) would come from the bulk download plus the `oppdateringer` change feed, written with
`INSERT … ON CONFLICT` or `COPY` instead of tracked entities. Units that disappear from Brreg
are not handled; a complete version would mark them deleted from the change feed.

### Data access

- **EF Core with migrations** (`src/CompanyRegistry.Api/Data/Migrations`). Tables and columns
  are snake_case, so the database reads naturally in `psql`.
- **The organisation number is the primary key.** It is the natural, stable identifier, and it
  is what the upsert matches on.
- **Controllers use `DbContext` directly.** `DbContext` is already a unit of work and `DbSet` is
  already a repository. In an API this small, a repository layer on top would only forward calls.
- **Read queries use `AsNoTracking()`** and project straight into DTOs with `Select`, so
  only the needed columns are fetched and nothing is tracked for a read.
- **The search is composed as an `IQueryable`.** Each filter adds a `Where`, and nothing runs
  until `CountAsync` / `ToListAsync`, which send one SQL statement each. The name search uses
  `ILIKE` with escaped wildcards, so a `%` in user input is treated as text.

### Authentication and authorisation

- **Authentication** (who is calling) is JWT bearer validation: issuer, audience, lifetime and
  signature.
- **Authorisation** (what they may do) is by policy. Each policy requires a scope in the token's
  `scope` claim: `companies.read` to search, `companies.import` to start imports. A reader token
  that tries to import gets 403, and a missing or invalid token gets 401.
- **Secure by default.** A fallback policy requires a valid token on every endpoint, so an
  endpoint someone forgot to annotate is closed, not open. Only `/health`, the token endpoint
  and the docs opt out.

**How this maps to Maskinporten.** Maskinporten is an OAuth 2.0 server for machine-to-machine
access between organisations, and the flow has the same shape as here:

| | This project | With Maskinporten |
|---|---|---|
| Client proves who it is | `client_id` + `client_secret` to `/connect/token` | A JWT grant signed with the organisation's certificate or key |
| Token issued by | `DevTokenIssuer` (HMAC, local only) | Maskinporten (RS256), with the scopes the client has been granted |
| API validates the token | Symmetric key from config | Maskinporten's public keys, fetched from its metadata/JWKS endpoint |
| API checks access | Policy requires a scope | Unchanged: same policies, same `scope` claim |

To move to Maskinporten you would delete the token endpoint and `DevTokenIssuer`, and configure
`AddJwtBearer` with Maskinporten's `Authority` and the API's audience. The controllers and
policies stay as they are. This project does not integrate with Maskinporten itself.

### Logging and traceability

- **Structured JSON logs** through `ILogger<T>` and message templates, so `{OrganisationNumber}`
  or `{RowsRejected}` become fields you can search on, not text inside a sentence.
- **Correlation id per request.** It is taken from `X-Correlation-ID` if the caller sends a safe
  value, or generated otherwise. It is returned in the response header, attached to every log
  line in the request, and included in error responses.
- **Traceability across the background boundary.** The id that started an import is stored on
  the `import_runs` row, and the worker logs the whole run under it. One id leads from the HTTP
  call to every line the import wrote.

In a larger system I would use OpenTelemetry and W3C `traceparent` for this. A correlation id
header is the same idea at a size that fits this project.

### Tests

- **Unit tests** for the MOD11 validator: real numbers, wrong check digits, wrong lengths, and
  the prefixes where no valid check digit exists.
- **Integration tests** run the real application in memory with `WebApplicationFactory`,
  against a real Postgres started by Testcontainers, with Brreg replaced by a fake HTTP handler.
  They cover 401, 403, scope limiting, search, MOD11 rejection on the endpoint, and the full
  import pipeline: rejecting a bad row, inserting, then updating one row and leaving the other
  untouched on a second run.

CI (GitHub Actions) builds with warnings as errors and runs all tests on every push.

## Layout

```
src/CompanyRegistry.Api/
  Program.cs            DI registrations and the middleware pipeline
  Controllers/          Companies, Imports, Token
  Data/                 Entities, AppDbContext, migrations
  Domain/               OrganisationNumber (MOD11)
  Import/               BrregClient, CompanyImporter, ImportQueue, ImportWorker, ImportScheduler
  Auth/                 Scopes and policies, AuthOptions, DevTokenIssuer
  Infrastructure/       Correlation id middleware, OpenAPI bearer scheme
tests/CompanyRegistry.Tests/
```

## Not done here

The project is local only: it is not deployed anywhere, and there is no real Maskinporten
integration. Migrations run on startup for convenience. In a deployed setup they would run as an
EF migration bundle in the release pipeline, and secrets would come from a vault, not from config
files.
