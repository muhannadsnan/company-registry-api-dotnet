# C#/.NET core concepts, mapped from PHP, Vue and Node

Thirteen core concepts of ASP.NET Core and EF Core, as used in this repo. For each: what it is, an analogy
from PHP/CodeIgniter, Vue/Nuxt or Node, where it appears in the code, and a common question about it.

---

## 1. Dependency injection and the service container

**In short:** `builder.Services` is a registry of "when someone asks for X, build it like this". Classes
declare what they need in their constructor, and the container builds and passes those things in.
Nobody calls `new` on services.

**Analogy:** CodeIgniter's `$this->load->model('company_model')`, except it's automatic and
typed. You don't load anything; you ask for it in the constructor.

**Lifetimes:**
- `Singleton`: one instance for the whole app (`ImportQueue`, so producer and consumer share one channel).
- `Scoped`: one instance per HTTP request (`AppDbContext`, `CompanyImporter`).
- `Transient`: new every time it's asked for.

**In repo:** `Program.cs`, the top half. `CompaniesController(AppDbContext db)` is a *primary constructor*.
The container sees it needs a `AppDbContext` and passes one in.

**Common question:** *"Why does `ImportWorker` use `IServiceScopeFactory` instead of taking `AppDbContext` directly?"*
→ The worker is a singleton that lives forever; DbContext is scoped and not thread-safe. Injecting a scoped
service into a singleton is a **captive dependency**: one DbContext would live forever and grow. So the
worker creates a fresh scope per import run.

## 2. Middleware and the request pipeline

**In short:** Every request passes through a chain of middleware in the order they're registered with `app.Use…`.
Each one can do something before and after calling the next. Order matters: auth must come before the endpoints.

**Analogy:** CodeIgniter hooks (`pre_controller`, `post_controller`), but as an explicit, ordered chain.
Or Nginx: a request passes through layers before it hits PHP-FPM.

**In repo:** `Program.cs`, bottom half, and `Infrastructure/CorrelationIdMiddleware.cs`. It runs first, so
every later log line (including exceptions) carries the correlation id.

**Common question:** *"What happens if you put `UseAuthorization` before `UseAuthentication`?"* → Authorization would run
before anyone has read the token, so every user looks anonymous. Every protected endpoint gives 401.

## 3. async/await and Task

**In short:** `await` frees the thread while waiting for I/O (HTTP, database). The thread goes back to the pool
and serves other requests; the method continues when the result arrives. `Task<T>` is "a T that will
arrive later", like a JS Promise.

**Analogy:** Exactly JS `async/await` and Promises from Nuxt and Node. The difference from PHP:
PHP-FPM blocks one worker process per request; ASP.NET serves many requests on a few threads, *if* you don't block them.

**In repo:** Everywhere I/O happens: `GetFromJsonAsync`, `ToListAsync`, `SaveChangesAsync`. `CancellationToken`
is passed down so a closed request or a stopping app cancels the database query or HTTP call.

**Common question:** *"Why not `.Result` or `.Wait()`?"* → It blocks a thread while waiting (thread pool starvation
under load), and can deadlock in some contexts. "Async all the way down."

## 4. EF Core: DbContext, DbSet, migrations, change tracking, AsNoTracking

**In short:**
- `DbContext` = a session with the database plus a unit of work. `DbSet<Company>` = the table as something
  you can query.
- **Migrations** = schema changes generated as C# from the model, applied in order and recorded in `__EFMigrationsHistory`.
  Like versioned SQL migration files, but generated from your entity classes.
- **Change tracking:** entities loaded through the context are remembered with their original values.
  `SaveChanges()` compares and writes only the columns that changed. An unchanged entity gives no SQL.
- **`AsNoTracking()`:** for reads; skip the bookkeeping because you won't save anything.

**Analogy:** CodeIgniter's Query Builder + hand-written migration scripts, but the ORM knows what changed.

**In repo:** `Data/AppDbContext.cs`, `Data/Migrations/`. The upsert in `Import/CompanyImporter.cs` uses change tracking:
`db.Entry(company).State == EntityState.Modified` tells you whether anything really changed. That's why
run #2 reported 5568 unchanged, 0 updated.

**Common question:** *"Would you use this for a million rows?"* → No. Tracked entities cost memory and per-row work.
At that scale I'd do `INSERT … ON CONFLICT` via raw SQL or `COPY`, which is what a bulk import needs.
EF Core 7+ also has `ExecuteUpdate`/`ExecuteDelete` for set-based updates (used in `ImportWorker`).

## 5. LINQ and IQueryable: when does the query hit the database?

**In short:** `IQueryable<T>` is a query that hasn't run yet. `Where`, `OrderBy`, `Select` just build an expression
tree. It's translated to SQL and executed only when you *materialise* it: `ToListAsync`, `CountAsync`,
`FirstOrDefaultAsync`, or a `foreach`.

**Analogy:** CodeIgniter's `$this->db->where(...)->like(...)` chain, which does nothing until `->get()`.

**In repo:** `CompaniesController.Search`. Filters are added conditionally to `companies`; `CountAsync` and `ToListAsync`
send exactly two SQL statements.

**Common question:** *"What's the difference between `IQueryable` and `IEnumerable`?"* → `IQueryable` runs in the database (SQL);
`IEnumerable` runs in memory in C#. Calling `.ToList()` too early, then filtering, means loading the whole
table and filtering in C#, a classic performance bug.

## 6. Minimal APIs vs controllers

**In short:** Controllers are classes grouping related endpoints with attributes for routes and auth. Minimal APIs map
lambdas directly: `app.MapGet("/x", handler)`. Same runtime, same DI, same auth; different organisation.

**Why controllers here:** they're what most existing enterprise code uses, they group related endpoints, and
`[ApiController]` gives automatic validation (400 with problem details) for free. Minimal APIs are lighter
and Microsoft's newer default; this repo uses them for `/health` and `/docs`.

**Analogy:** Controllers = CodeIgniter controllers. Minimal APIs = Nuxt `server/api/*.ts` handlers.

## 7. Records, nullable reference types, `required`

**In short:**
- `record`: a class for data with value equality, immutability by default, and a short syntax. Used for DTOs:
  `CompanyDto`, `BrregEnhet`, `PagedResult<T>`.
- **Nullable reference types:** `string` means "never null", `string?` means "may be null", and the compiler warns
  if you might dereference null. It's on in the `.csproj` (`<Nullable>enable</Nullable>`).
- `required`: the compiler forces you to set the property when you create the object (`Company.OrganisationNumber`).

**Analogy:** TypeScript's `string | null` and strict null checks.

**In repo:** `Contracts/`, `Import/BrregModels.cs` (everything from Brreg is `?`, because external data is
untrusted), `Data/Company.cs`.

## 8. IHttpClientFactory, and why not `new HttpClient()`

**In short:** `new HttpClient()` per call leaks sockets (they stay in TIME_WAIT). A single static one never sees DNS
changes. The factory pools and recycles the underlying handlers, and it's where you attach base address, headers
and resilience (retries, timeouts, circuit breaker) in one place.

**In repo:** `Program.cs`: `AddHttpClient<BrregClient>(…).AddStandardResilienceHandler()`. This is a *typed client*:
`BrregClient` gets a ready-configured `HttpClient` injected.

**Common question:** *"What does the resilience handler do?"* → Retries transient failures (5xx, timeouts) with exponential backoff and
jitter, a per-attempt and a total timeout, and a circuit breaker that stops hammering a service that is down. It's built on Polly.

## 9. BackgroundService / IHostedService: the cron equivalent

**In short:** A hosted service starts with the app and runs alongside the web server in the same process.
`BackgroundService` is the base class: you override `ExecuteAsync` and loop until the stopping token fires.

**Analogy:** cron jobs and systemd timers, but in-process and sharing DI, config and logging with the API.

**In repo:** `Import/ImportWorker.cs` (consumes the queue), `Import/ImportScheduler.cs` (`PeriodicTimer`, the
"cron line", off by default).

**Common question:** *"What if the app runs on three instances?"* → Each runs its own scheduler and in-memory queue, so you'd
get three imports. Fixes: a database lock or lease, a real message queue, or run the job outside the API
(Azure Function, Kubernetes CronJob). This project is single-instance by design.

## 10. Configuration and options: appsettings.json, IOptions<T>

**In short:** Configuration is layered: `appsettings.json` → `appsettings.{Environment}.json` → environment variables →
command line, with later layers winning. The **options pattern** binds a section to a typed class (`ImportOptions`)
and injects it as `IOptions<ImportOptions>`. `ValidateDataAnnotations().ValidateOnStart()` makes the app refuse
to start with invalid config.

**Analogy:** CodeIgniter's `config/*.php` + `.env`, but typed and validated.

**In repo:** `appsettings*.json`, `Import/ImportOptions.cs`, `Auth/AuthOptions.cs`. Env var override: `Import__PageSize=200`
(double underscore = section separator).

## 11. Authentication vs authorisation, policies, claims, scopes

**In short:**
- **Authentication** = who are you? Validate the token: signature, issuer, audience, expiry. The result is a
  `ClaimsPrincipal`: the caller as a set of claims (`sub`, `client_id`, `scope`).
- **Authorisation** = what may you do? A **policy** is a named rule (`companies.read`) evaluated against the
  claims. `[Authorize(Policy = …)]` on a controller applies it.
- **Scopes** = permissions granted to a *client* (machine), carried in the `scope` claim. 401 = not authenticated,
  403 = authenticated but not allowed.

**Maskinporten in one breath:** client-credentials OAuth for organisations. The client signs a JWT grant with its
certificate and gets an access token with the scopes Digdir has granted it. The API validates the token against
Maskinporten's public keys and checks the scope. In this repo, swapping the local token issuer for Maskinporten changes
the `AddJwtBearer` configuration only; policies and controllers stay the same.

**In repo:** `Auth/Scopes.cs` (`RequireScope`), `Program.cs` (policies + fallback policy), `Auth/DevTokenIssuer.cs`.

**Common question:** *"Why `MapInboundClaims = false`?"* → By default .NET renames JWT claims to long legacy URIs
(`sub` → `http://schemas.xmlsoap.org/.../nameidentifier`). Turning it off keeps claim names as they are in the token.

## 12. ILogger<T> and structured logging

**In short:** `ILogger<CompanyImporter>` is injected; the `T` becomes the log category. You log with a **message
template**, `"Rejected {OrganisationNumber}: {Reason}"`, not string interpolation, so each placeholder
becomes a separate field in the JSON. **Scopes** (`BeginScope`) attach fields like `CorrelationId` to every log line
inside them.

**Analogy:** the difference between `log_message('error', "Rejected $orgnr")` and writing JSON rows you can
filter on.

**Common question:** *"Why not `$"Rejected {orgnr}"`?"* → Interpolation produces a finished string: the field is lost,
and the string is built even when that log level is off.

## 13. xUnit: facts, theories, WebApplicationFactory

**In short:**
- `[Fact]` = one test, no parameters. `[Theory]` + `[InlineData(...)]` = same test, many inputs (the MOD11 tests).
- Test classes get dependencies through the constructor too. `IClassFixture` / `ICollectionFixture` share
  expensive setup (here: one Postgres container for all integration tests).
- `WebApplicationFactory<Program>` boots the real app in memory with a test HTTP client. It's the full pipeline
  (middleware, auth, DI, EF) without opening a port. `ConfigureTestServices` swaps pieces; here Brreg is replaced by a fake handler.

**Analogy:** PHPUnit data providers = theories. WebApplicationFactory ≈ functional tests that hit your routes
without a running Nginx.

**In repo:** `tests/CompanyRegistry.Tests/`. Testcontainers starts a real Postgres in Docker, so `ILIKE` and migrations are
tested against the real database, not an in-memory fake.

---

## Smaller things worth knowing

- **ProblemDetails:** standard JSON error format (RFC 9457). `[ApiController]` returns it for validation errors; we add `correlationId`.
- **`Channel<T>`:** an in-process, async producer/consumer queue. A stand-in for RabbitMQ or Service Bus inside one process.
- **Primary constructors** (C# 12): `class X(AppDbContext db)`. Constructor parameters available in the whole class.
- **`sealed`:** the class can't be inherited. A common default for classes not designed for inheritance.
- **`.slnx`:** the new XML solution file format (.NET 9+), replacing the old `.sln`.
- **Event-based architecture / outbox:** if this API had to publish "company changed" events, writing the event in
  the same transaction as the change (an outbox table) and having a worker publish it avoids losing events when the
  broker is down. It isn't built here, but the `import_runs` table + worker is the same shape.

## The three flows, in brief

1. **Architecture:** request → middleware (correlation id, errors, auth) → controller → EF Core → Postgres.
   Imports go through a queue to a background worker so the request returns at once.
2. **Auth flow:** client credentials → token with scopes → JWT validated → policy checks scope → 401/403/200.
   Maskinporten replaces the token issuer, not the policies.
3. **Import design:** typed HttpClient with retries → page by page → validate (MOD11) → upsert via change tracking →
   commit per page → counts in `import_runs`, logged under the correlation id that started it.


## What the project demonstrates, one line each

- **Middleware**: the ordered chain every request passes through (correlation ID, errors, authentication, authorization).
- **DI**: classes ask for what they need in their constructor, and the container provides it.
- **Options**: typed, validated config classes bound from appsettings.json (IOptions<ImportOptions>).
- **Migrations**: versioned schema changes generated from the entities and applied in order.
- **LINQ**: C# query syntax that EF Core translates to SQL and runs only when materialised.
- **Change tracking**: EF remembers loaded values and writes only what actually changed.
- **Background services**: long-running workers inside the app; here, the import worker and the scheduler.
- **Logging**: structured JSON logs with named fields, via ILogger<T>.
- **Traceability**: one correlation ID follows a request from HTTP into the background job and the import_runs row.