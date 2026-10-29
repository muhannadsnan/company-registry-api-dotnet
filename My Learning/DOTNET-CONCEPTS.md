# C#/.NET core concepts, mapped from PHP, Vue and Node

Thirteen core concepts of ASP.NET Core and EF Core, as used in this repo. For each: what it is, an analogy
from PHP/CodeIgniter, Vue/Nuxt or Node, where it appears in the code, and a common question about it.
Each section ends with **In my words**: my own answer, corrected after an explain-back review.

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

**In my words:**
> The DI container builds it. I register services in Program.cs with a lifetime, and the container passes them into
> constructors. Scoped means one per request, so each request gets a fresh DbContext. Singleton means one shared
> instance, created the first time something asks for it, like the import queue that the controller writes to and the
> worker reads from. A hosted service like the worker starts with the app and lives forever, so it creates its own
> scope per run to get a fresh DbContext.

## 2. Middleware and the request pipeline

**In short:** Every request passes through a chain of middleware in the order they're registered with `app.Use…`.
Each one can do something before and after calling the next. Order matters: auth must come before the endpoints.

**Analogy:** CodeIgniter hooks (`pre_controller`, `post_controller`), but as an explicit, ordered chain.
Or Nginx: a request passes through layers before it hits PHP-FPM.

**In repo:** `Program.cs`, bottom half, and `Infrastructure/CorrelationIdMiddleware.cs`. It runs first, so
every later log line (including exceptions) carries the correlation id.

**Common question:** *"What happens if you put `UseAuthorization` before `UseAuthentication`?"* → Authorization would run
before anyone has read the token, so every user looks anonymous. Every protected endpoint gives 401.

**In my words:**
> Middleware is a chain of layers every request goes through, in the order I add them. Each one can act before and
> after the next, or stop the request and answer itself, like auth returning 401. So order matters: authentication
> must come before authorization, or no user is known yet. The correlation id goes first, so every later log line and
> every response carries it, even when a later layer stops the request early.

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

**In my words:**
> await frees the thread while we wait for I/O like the database or an HTTP call. The thread is a waiter and the
> database is the kitchen: the waiter doesn't stand at the kitchen door, but goes back to the pool and serves other
> requests, so the server handles many more requests at once. Blocking with .Result holds a thread doing nothing, and
> under load you run out of threads. Task is like a JS Promise. The CancellationToken is the "stop" signal: if the
> client disconnects or the app shuts down, the query or HTTP call is cancelled instead of finishing for nobody.

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

**In my words:**
> Change tracking means EF keeps a snapshot of what it loaded, so at SaveChanges it updates only the columns that
> changed, and writes nothing if nothing changed. The importer needs that for its upsert. Reads don't change anything,
> so AsNoTracking skips the snapshot. A migration is one schema change, generated from my entities, and the history
> table records which ones a database has run. An applied migration never runs again on that database, so editing it
> means my existing databases and fresh ones quietly differ, and forcing it to rerun conflicts with the existing tables.
> Changes always go in a new migration.

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

**In my words:**
> LINQ on an IQueryable only builds the query. Each Where and OrderBy adds to the SQL, and nothing runs until I call
> ToListAsync or CountAsync, like ->get() in Laravel. I filter and sort first, then Skip and Take for the page, and
> Select into the DTO last: the DTO is built by a constructor, and EF can't see inside it to map its properties back
> to columns, so it can't translate steps that come after it.

## 6. Minimal APIs vs controllers

**In short:** Controllers are classes grouping related endpoints with attributes for routes and auth. Minimal APIs map
lambdas directly: `app.MapGet("/x", handler)`. Same runtime, same DI, same auth; different organisation.

**Why controllers here:** they're what most existing enterprise code uses, they group related endpoints, and
`[ApiController]` gives automatic validation (400 with problem details) for free. Minimal APIs are lighter
and Microsoft's newer default; this repo uses them for `/health` and `/docs`.

**Analogy:** Controllers = CodeIgniter controllers. Minimal APIs = Nuxt `server/api/*.ts` handlers.

**In my words:**
> Same engine, two styles. A minimal API maps each endpoint with a lambda, like Express. Controllers are classes that
> inherit ControllerBase and group related endpoints, and the [ApiController] attribute gives automatic model
> validation and 400 responses. Both share the same DI, middleware and auth. New templates default to minimal APIs.
> I chose controllers because they keep a growing API organised and most existing .NET codebases use them.

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

**In my words:**
> A record is a data type with value equality: two records with the same values are equal (like PHP ==), while two
> class instances are equal only if they are the same object. It's filled once through its constructor and is
> read-only after that; `with` makes a changed copy. That fits a DTO: a contract I build and send, never change. The
> entity is a class because EF tracks changes by modifying it, and its identity is its key, not its values.
> `?` and `required` are compile-time checks: `?` marks what may be null so the compiler warns where a null could slip
> through, and `required` makes it a build error to create the object without that field. EF also uses them for
> NULL / NOT NULL columns. Checking the caller's input at runtime is a different tool: validation attributes like [Range].

## 8. IHttpClientFactory, and why not `new HttpClient()`

**In short:** `new HttpClient()` per call leaks sockets (they stay in TIME_WAIT). A single static one never sees DNS
changes. The factory pools and recycles the underlying handlers, and it's where you attach base address, headers
and resilience (retries, timeouts, circuit breaker) in one place.

**In repo:** `Program.cs`: `AddHttpClient<BrregClient>(…).AddStandardResilienceHandler()`. This is a *typed client*:
`BrregClient` gets a ready-configured `HttpClient` injected.

**Common question:** *"What does the resilience handler do?"* → Retries transient failures (5xx, timeouts) with exponential backoff and
jitter, a per-attempt and a total timeout, and a circuit breaker that stops hammering a service that is down. It's built on Polly.

**In my words:**
> new HttpClient() per call exhausts sockets under load, because closed sockets linger for minutes. One static client
> forever never notices DNS changes. IHttpClientFactory pools connections and recycles them, which fixes both. A typed
> client like BrregClient also keeps the base address, headers and resilience in one place in Program.cs. The standard
> resilience handler adds retries with backoff for temporary errors, timeouts, and a circuit breaker that stops calling
> a failing service for a while.

## 9. BackgroundService / IHostedService: the cron equivalent

**In short:** A hosted service starts with the app and runs alongside the web server in the same process.
`BackgroundService` is the base class: you override `ExecuteAsync` and loop until the stopping token fires.

**Analogy:** cron jobs and systemd timers, but in-process and sharing DI, config and logging with the API.

**In repo:** `Import/ImportWorker.cs` (consumes the queue), `Import/ImportScheduler.cs` (`PeriodicTimer`, the
"cron line", off by default).

**Common question:** *"What if the app runs on three instances?"* → Each runs its own scheduler and in-memory queue, so you'd
get three imports. Fixes: a database lock or lease, a real message queue, or run the job outside the API
(Azure Function, Kubernetes CronJob). This project is single-instance by design.

**In my words:**
> A BackgroundService is a hosted service that starts with the app and lives in the same process, the .NET version
> of a cron job or a queue worker. The controller writes a run id with `queue.EnqueueAsync(run.Id, ct)`, and the worker,
> awaiting `foreach` over `queue.ReadAllAsync()`, picks it up and runs the import in its own DI scope. Awaiting the
> channel holds no thread while the queue is empty. The queue lives in memory, so if the app stops, queued run ids are
> lost and the running import stays "Running". Pages already saved stay, and the upsert makes a re-run safe. The
> defence is to mark Running and Queued runs as Abandoned on startup. For more than one instance I'd move the queue to
> the database or a message broker.

## 10. Configuration and options: appsettings.json, IOptions<T>

**In short:** Configuration is layered: `appsettings.json` → `appsettings.{Environment}.json` → environment variables →
command line, with later layers winning. The **options pattern** binds a section to a typed class (`ImportOptions`)
and injects it as `IOptions<ImportOptions>`. `ValidateDataAnnotations().ValidateOnStart()` makes the app refuse
to start with invalid config.

**Analogy:** CodeIgniter's `config/*.php` + `.env`, but typed and validated.

**In repo:** `appsettings*.json`, `Import/ImportOptions.cs`, `Auth/AuthOptions.cs`. Env var override: `Import__PageSize=200`
(double underscore = section separator).

**In my words:**
> Configuration is layered: appsettings.json, then the environment's appsettings file, then user secrets in
> Development, then environment variables, then command-line arguments. The later source wins, so one image runs
> everywhere and only the environment changes. .NET doesn't read .env files; the host sets real environment variables
> before the app starts. Locally, secrets live in user secrets, outside the repo. In production they come from the
> host's environment variables, or from a vault like Key Vault that the app reads at startup, which adds rotation in
> one place, an audit log and access per secret. Never in git, appsettings.json or the image. I bind sections to typed
> options classes and validate them on start, so a missing setting stops the app immediately.

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

**In my words:**
> Authentication is who you are: the API validates the JWT's signature, issuer, audience and expiry. If that fails,
> 401. Authorisation is what you may do: a policy checks the token's claims. A claim is any fact in the token, like
> client_id or exp, and the scope claim lists the permissions. The import endpoint's policy requires the
> companies.import scope, so a valid reader token gets 403. A JWT is signed, not encrypted: anyone can read the claims,
> but nobody can change them without breaking the signature. Maskinporten works the same way: it issues the token with
> the scopes, and the API checks them.

## 12. ILogger<T> and structured logging

**In short:** `ILogger<CompanyImporter>` is injected; the `T` becomes the log category. You log with a **message
template**, `"Rejected {OrganisationNumber}: {Reason}"`, not string interpolation, so each placeholder
becomes a separate field in the JSON. **Scopes** (`BeginScope`) attach fields like `CorrelationId` to every log line
inside them.

**Analogy:** the difference between `log_message('error', "Rejected $orgnr")` and writing JSON rows you can
filter on.

**Common question:** *"Why not `$"Rejected {orgnr}"`?"* → Interpolation produces a finished string: the field is lost,
and the string is built even when that log level is off.

**In my words:**
> With a message template, the values stay as named fields, so a log tool can filter, group and sum on them, like all
> imports where Rejected is above zero. String interpolation turns them into plain text, which only regex can search.
> JSON is just one way to print those fields. The correlation id is one of them, attached to every line of a request,
> so when a user reports an error with that id, I find exactly that request's logs among all the concurrent ones, even
> into the background job. That is traceability.

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

**In my words:**
> A Fact is a single test. A Theory runs the same test for each InlineData row, for when only the data changes. Unit
> tests check one piece of logic, like MOD11, across all its edge cases in milliseconds. Integration tests boot the
> real app with WebApplicationFactory against a real Postgres container from Testcontainers, and catch what unit tests
> can't: routing, auth, middleware, EF and SQL, and migrations. So I put edge cases in unit tests and one integration
> test per behaviour to prove it's wired up (the test pyramid). A test only checks what it calls, so an endpoint
> without a test is untested, and a test must fail for the reason its name says.

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