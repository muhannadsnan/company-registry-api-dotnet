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
- `Singleton`: one instance for the whole app (`ImportQueue`, so producer and consumer share one channel; a
  stateless `TokenService` whose only dependency is `IOptions<T>`). Pick it when there's no per-request state and
  every dependency is a singleton too.
- `Scoped`: one instance per HTTP request (`AppDbContext`, `CompanyImporter`).
- `Transient`: new every time it's asked for.

**In repo:** `Program.cs`, the top half. `CompaniesController(AppDbContext db)` is a *primary constructor*.
The container sees it needs a `AppDbContext` and passes one in.

**Common question:** *"Why does `ImportWorker` use `IServiceScopeFactory` instead of taking `AppDbContext` directly?"*
→ The worker is a singleton that lives forever; DbContext is scoped and not thread-safe. Injecting a scoped
service into a singleton is a **captive dependency**: one DbContext would live forever and grow. So the
worker creates a fresh scope per import run.

**In my words:**
> **Lifetimes.** Scoped means one instance per request, shared by everything during that request. Singleton means
> one instance for the whole app, shared by everything and created the first time something asks for it. Transient
> means a new instance every time something asks for it.
>
> **Why each one here.** AppDbContext is scoped because it isn't thread-safe: two requests sharing one at the same
> moment could mix up their changes or throw. TokenService is a singleton because it's stateless, with no
> per-request data in fields, so sharing it is safe, and one instance is cheaper.
>
> **Captive dependency.** A singleton takes only singletons in its constructor, never a scoped service: a captured
> AppDbContext would live as long as the app, its change tracker would grow forever, and requests could mix data. In
> Development the app throws at startup to prevent that.
>
> **In the worker.** ImportWorker takes IServiceScopeFactory, a singleton, and creates a new scope per run; the
> DbContext comes from that scope, and `await using` disposes it at the end of each loop.
>
> **One object, two types.** Each registration creates its own instance, so `AddSingleton<ImportQueue>()` plus
> `AddSingleton<IImportQueue, ImportQueue>()` would give two queues: the service writes a run id into queue A, and
> the worker reads queue B and never finds it. The lambda `sp => sp.GetRequiredService<ImportQueue>()` gives
> whoever asks for the interface the ImportQueue that already exists: one object, reachable under two types.
>
> **Why an interface.** ImportRunService depends on IImportQueue, so Program.cs can switch the queue (in-memory or
> RabbitMQ) through config without changing ImportRunService, and tests can pass in a fake queue.

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
> **The onion.** The pipeline is a chain of middleware layers, run in the order I add them, like an onion. Each
> layer gets the request and a `next`: the code before `await next` runs on the way in, the code after it runs on
> the way out, after the controller has answered, so the layers finish in reverse order.
>
> **Short-circuit.** A layer can also send the response itself and skip `next`: UseAuthorization answers 401
> without a valid token, or 403 when the token lacks the scope, and the controller never runs. (The 400 for a
> missing [Required] field is not middleware: [ApiController] model validation runs inside the endpoint.)
>
> **Why correlation goes first.** CorrelationIdMiddleware goes first so it covers every layer after it: its log
> scope puts the request's id on every later log line, errors included; every response gets the X-Correlation-ID
> header, even one a later layer stops early, like a 401; and its timer and `finally` see the whole pipeline, the
> method and the final status code.
>
> **Order matters.** If UseAuthorization came before UseAuthentication, nobody would have read the token yet, so
> every caller would look anonymous: protected endpoints answer 401 even with a valid token, and only
> [AllowAnonymous] ones work.

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
> **What await does.** await frees the thread while we wait for I/O like the database or an HTTP call: it goes back
> to the pool and serves other requests, and the method continues when the result arrives. That matters more in
> ASP.NET than in PHP-FPM: PHP gives each request its own process, while ASP.NET serves many requests on a few
> shared threads, so a thread sitting and waiting is wasted for everyone.
>
> **Blocking.** .Result and .Wait() block the thread until the task finishes; under load the pool runs out of
> threads (thread pool starvation), and in UI apps or old ASP.NET they can deadlock (the task waits for the context
> the blocked thread holds). The rule is async all the way down.
>
> **Concurrency.** Calling an async method starts the work at once and returns a Task, like a JS Promise:
> RoleImporter starts five Brreg calls, then awaits Task.WhenAll, so they run at the same time (about 200 ms instead
> of 5 × 200 ms); ChunkSize caps how many run at once.
>
> **Cancellation.** The CancellationToken is the stop signal for work in progress: when the user closes the browser,
> the running query or HTTP call is cancelled instead of finishing for nobody, and when the app shuts down,
> ImportWorker's stoppingToken stops the import at the next await. Cancelling never "finishes the write": the page in
> progress is dropped (it throws OperationCanceledException), and only pages committed before it stay. The final
> save uses CancellationToken.None because ct is already cancelled by then and would make that save throw too; with
> None, the run's status (Cancelled) is still written and the row isn't left Running.

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
> **Change tracking.** Change tracking means EF keeps a snapshot of the original values of every entity it loads. At
> SaveChanges it compares each entity with its snapshot and sends an UPDATE with only the changed columns, nothing
> at all for an unchanged one; added entities become INSERTs. CompanyImporter loads the page's existing companies,
> assigns the Brreg values and asks `db.Entry(company).State`: Modified counts as updated, anything else as
> unchanged.
>
> **AsNoTracking and Clear.** Reads change nothing, so AsNoTracking skips the snapshots: less memory, faster
> queries. ChangeTracker.Clear() after each page forgets that page's entities, so the tracker doesn't grow to all
> 7,000 companies and each SaveChanges compares only one page.
>
> **Migrations.** A migration is one schema change generated from my entities, and the history table records which
> ones a database has run, so an applied migration never runs again there: editing it means my database and fresh
> ones quietly differ. If it was only local, I roll back with `dotnet ef database update <PreviousMigration>`, then
> `dotnet ef migrations remove`, fix the model and add it again; if it was pushed or ran anywhere else, I add a new
> migration that corrects it.
>
> **Bulk changes.** For a million rows I wouldn't use tracked entities: I'd bulk-load with Postgres COPY into a
> staging table and run one INSERT ... ON CONFLICT (orgnr) DO UPDATE. ExecuteUpdate/ExecuteDelete are for set-based
> changes with one rule for every row, like marking unfinished import runs Abandoned on startup.

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
> **IQueryable.** An IQueryable is a query that hasn't run yet: Where, OrderBy, Skip/Take and Select on it only
> build the SQL, like Laravel's query builder before ->get(). It runs when I materialise it with ToListAsync,
> CountAsync, FirstOrDefaultAsync or a foreach.
>
> **Search.** Search sends exactly two statements, CountAsync for the total and ToListAsync for the page, both with
> the same WHERE.
>
> **IEnumerable.** The result is a List, an IEnumerable in memory, and LINQ on it runs in C#, like a Laravel
> Collection after ->get(): the same method names, a different place. So `db.Companies.ToList().Where(...)` loads
> the whole table and filters in C#: filter first, materialise last.
>
> **Select last.** Select into the DTO comes last, because EF translates by mapping entity properties to columns,
> and after a constructor call it can't see which argument is which column, so a later Where or OrderBy throws at
> runtime.
>
> **What EF can't translate.** RoleTypes.RankOf is my own C# method with a Dictionary, which Postgres doesn't know,
> so EF can't translate an OrderBy on it: GetRoles loads the company's roles and sorts them in memory, which is fine
> for a handful of rows. For many rows I'd store the rank in a column and sort in SQL.

## 6. Minimal APIs vs controllers

**In short:** Controllers are classes grouping related endpoints with attributes for routes and auth. Minimal APIs map
lambdas directly: `app.MapGet("/x", handler)`. Same runtime, same DI, same auth; different organisation.

**Why controllers here:** they're what most existing enterprise code uses, they group related endpoints, and
`[ApiController]` gives automatic validation (400 with problem details) for free. Minimal APIs are lighter
and Microsoft's newer default; this repo uses them for `/health` and `/docs`.

**Analogy:** Controllers = CodeIgniter controllers. Minimal APIs = Nuxt `server/api/*.ts` handlers.

**In my words:**
> **Two styles.** Same engine, two ways to organise code; neither has features the other lacks. Both get the same
> DI, middleware pipeline, routing, model binding, auth ([Authorize] / RequireAuthorization) and ProblemDetails. A
> minimal API registers each route in Program.cs with a lambda, `app.MapGet("/x", (AppDbContext db) => ...)`, like a
> Laravel route closure or Express; dependencies come in as lambda parameters. A controller is a class inheriting
> ControllerBase: routes come from attributes ([Route] on the class, [HttpGet]/[HttpPost] on the methods) and
> dependencies through the constructor, like a Laravel controller.
>
> **Why controllers.** I chose controllers because most existing enterprise .NET code uses them, so it's what I'll
> maintain at work, and a class groups related endpoints under one route prefix and one [Authorize]. With minimal
> APIs I'd use `app.MapGroup("/api/companies").RequireAuthorization("companies.read")`: the auth system's method
> instead of the attribute, and `.AllowAnonymous()` for one public endpoint, like `/health` in Program.cs.
>
> **What [ApiController] gives.** [ApiController] gives: automatic 400 validation, so a login without Username
> ([Required] on LoginRequest) gets a 400 ValidationProblemDetails response before Login runs, no exception and no
> ModelState check, like a Laravel FormRequest; binding inference, so LoginRequest comes from the JSON body without
> [FromBody]; ProblemDetails bodies for bare error results like NotFound(); and it requires attribute routing.
> [Authorize] and the 401 on a wrong password are not from it: [Authorize] works anywhere, and the 401 is my own
> Problem(statusCode: 401) call.

## 7. Records, nullable reference types, `required`

**In short:**
- `record`: a class for data with value equality, immutability by default, and a short syntax. Used for DTOs:
  `CompanyDto`, `BrregEnhet`, `PagedResult<T>`.
- **Nullable reference types:** `string` means "never null", `string?` means "may be null", and the compiler warns
  if you might dereference null. It's on in the `.csproj` (`<Nullable>enable</Nullable>`).
- `required`: the compiler forces you to set the property when you create the object (`Company.OrganisationNumber`).
- **Creating objects:** `new(a, b)` is *target-typed new* (C# 9): the type comes from the variable or return type.
  `new X { P = a }` is an *object initializer* (C# 3): the empty constructor, then property assignments, so the
  properties need `set` or `init`. It replaces constructors that only copy parameters into properties; `required`
  checks that the `{ }` sets them. `new { P = a }` (no type name) is an anonymous object. JS: `Object.assign(new X(), {...})`.

**Analogy:** TypeScript's `string | null` and strict null checks.

**In repo:** `Contracts/`, `Import/BrregModels.cs` (everything from Brreg is `?`, because external data is
untrusted), `Data/Company.cs`.

**In my words:**
> **Records.** A record is a class (a reference type) with extras the compiler writes: value equality (two records
> are == when all their values match), immutability by default (init-only, set once then read-only), `with` for a
> copy with changes (`req with { Username = "x" }`, like JS `{...req, username: "x"}`), a one-line declaration and a
> readable ToString.
>
> **DTOs vs entities.** Records fit DTOs (LoginRequest, BrregEnhet, CompanyDto): data created once and passed along.
> Entities like Company are classes because EF needs identity, a row is the same row because of its key (HasKey
> Orgnr), not because all values match, and mutability: I change company.Name, the change tracker compares it to the
> snapshot and writes an UPDATE. EF can map a record, but value equality and init-only properties fight change
> tracking.
>
> **Nullable.** With Nullable enabled, `string` means never null and `string?` may be null. The compiler warns when
> I assign null to a string or dereference a string? without checking. It's compile time only: at runtime a string
> can still be null (from JSON, `null!`), like TypeScript's strictNullChecks. EF also reads it for the column:
> ImportRun's `string? CorrelationId` gives a NULL column, so the migration just adds it and runs from before 12b
> keep NULL. A `string` would give NOT NULL, and the migration would need a default for the existing rows or fail.
>
> **required vs [Required].** `required` (C# keyword, Company.Orgnr) is checked by the compiler when my code creates
> the object: `new Company { Name = "x" }` without Orgnr doesn't compile. It checks that I set it, not the value
> (`null!` compiles). It protects my own code. `[Required]` (attribute, LoginRequest.Username) is checked at runtime
> by model validation when a request arrives: missing, null or "" makes the model invalid and [ApiController]
> returns a 400 before Login runs, like a Laravel FormRequest rule. It protects me from the client. Neither depends
> on class vs record.

## 8. IHttpClientFactory, and why not `new HttpClient()`

**In short:** `new HttpClient()` per call leaks sockets (they stay in TIME_WAIT). A single static one never sees DNS
changes. The factory pools and recycles the underlying handlers, and it's where you attach base address, headers
and resilience (retries, timeouts, circuit breaker) in one place.

**In repo:** `Program.cs`: `AddHttpClient<BrregClient>(…).AddStandardResilienceHandler()`. This is a *typed client*:
`BrregClient` gets a ready-configured `HttpClient` injected.

**Common question:** *"What does the resilience handler do?"* → Retries transient failures (5xx, timeouts) with exponential backoff and
jitter, a per-attempt and a total timeout, and a circuit breaker that stops hammering a service that is down. It's built on Polly.

**In my words:**
> **The problem.** The HttpClient object is cheap; the handler inside it (SocketsHttpHandler) owns the TCP
> connections. A new HttpClient per call means new connections every time, and after Dispose each closed socket
> stays in TIME_WAIT for a few minutes, so under load the app runs out of ports: socket exhaustion. One static
> client forever reuses connections, which is good, but it never looks up DNS again, so if Brreg moves to a new IP
> it keeps calling the old one.
>
> **The factory.** IHttpClientFactory keeps a pool of handlers, reuses each for a while (2 minutes by default) and
> then rotates it out: connection reuse and fresh DNS.
>
> **Typed client.** I never name IHttpClientFactory in practice2, because `AddHttpClient<BrregClient>(...)` registers the factory
> and tells DI: when someone needs a BrregClient, get an HttpClient from the factory with this configuration
> (BaseAddress, the User-Agent) and pass it to the constructor. That's a typed client: BrregClient just calls
> relative URLs, and all Brreg HTTP settings live in one place in Program.cs, like an axios.create({ baseURL })
> instance. The other style is a named client, `factory.CreateClient("brreg")`.
>
> **Resilience.** AddStandardResilienceHandler (Polly) adds retries on temporary failures (5xx, 408, 429, timeouts)
> with exponential backoff and jitter between attempts, a per-attempt and a total timeout, and a circuit breaker
> that, after many failures, stops calling for a while and fails fast.
>
> **The handler chain.** Outgoing requests pass through a chain of handlers, an onion like middleware: BrregClient →
> HttpClient → resilience handler → primary handler → network. In 13c, ConfigurePrimaryHttpMessageHandler swapped
> only the innermost piece for FakeBrregHandler; the resilience handler still wraps it, so a 503 from the fake would
> be retried.

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
> **What it is.** A BackgroundService is a class registered in Program.cs with `AddHostedService<ImportWorker>()`.
> It starts with the app (ExecuteAsync runs once at startup), lives in the same process, shares DI, config and
> logging with the API, and stops with the app. stoppingToken fires when the app shuts down (Ctrl+C, a deploy,
> SIGTERM); the worker passes it to the importers, so the import stops at the next await. Cancelling stops, it never
> completes: the page in progress is dropped, pages already committed stay, and the final status is saved with
> CancellationToken.None. It's like Laravel's `queue:work`, but inside the API process. A cron job is time-based (run at
> 02:00); ImportWorker is job-based: it waits on the Channel for run ids (a time-based one would loop on a
> PeriodicTimer).
>
> **202 + Channel.** POST /api/imports saves a Queued run row, puts its id in the Channel and returns 202 Accepted
> with Location /api/imports/{id}. Running the import inside the request would break three ways: it takes over a
> minute, so the client, proxy or load balancer times out; if the user closes the tab, RequestAborted cancels it;
> and the client gets no progress. With the hand-off, the import no longer depends on the HTTP connection, and the
> run row is the progress the client polls. While the queue is empty, `await foreach` over `queue.ReadAllAsync` is
> awaiting and holds no thread; the next EnqueueAsync wakes it.
>
> **Restart.** On a restart, the Running run is cancelled by stoppingToken partway: pages already saved stay (the
> upsert makes a re-run safe) and the row still says Running. The Queued run's id existed only in the in-memory
> Channel, so it's gone and the row says Queued forever. Because POST returns 409 while a run is Queued or Running,
> no import could start again. So ExecuteAsync first calls MarkUnfinishedRunsAbandonedAsync: one ExecuteUpdate sets
> them to Abandoned.
>
> **Three instances.** With three instances, each has its own Channel and worker: a restarting instance marks
> Abandoned a run another instance is still running; two POSTs at the same moment on two instances can both pass the
> 409 check; and a queued id dies with its instance. The fix is one shared queue: the database as the queue (claim a
> run with a row lock, `FOR UPDATE SKIP LOCKED`) or a message broker (RabbitMQ, Azure Service Bus).

## 10. Configuration and options: appsettings.json, IOptions<T>

**In short:** Configuration is layered: `appsettings.json` → `appsettings.{Environment}.json` → environment variables →
command line, with later layers winning. The **options pattern** binds a section to a typed class (`ImportOptions`)
and injects it as `IOptions<ImportOptions>`. `ValidateDataAnnotations().ValidateOnStart()` makes the app refuse
to start with invalid config.

**Analogy:** CodeIgniter's `config/*.php` + `.env`, but typed and validated.

**In repo:** `appsettings*.json`, `Import/ImportOptions.cs`, `Auth/AuthOptions.cs`. Env var override: `Import__PageSize=200`
(double underscore = section separator).

**In my words:**
> **Layers.** A setting like Jwt:SigningKey comes from layers, and the later one wins: appsettings.json,
> appsettings.{Environment}.json, user secrets (Development only), environment variables, command-line arguments.
> `Queue__Provider=RabbitMq dotnet run ...` (step 14) is an environment variable for that one command, not a
> command-line argument (that would be `-- --Queue:Provider=RabbitMq`); `__` stands for `:`, which many shells
> don't allow in variable names. It beats `"Provider": "InMemory"` because env vars come after appsettings.json.
>
> **Where the key comes from.** Locally it's Development, so the key comes from user secrets: the csproj's
> UserSecretsId names the folder ~/.microsoft/usersecrets/<id>/secrets.json, outside the repo. The tests run as
> UseEnvironment("Testing"), so user secrets aren't loaded even on my machine, and ApiFactory sets its own key with
> UseSetting; that also makes them work in CI. In production it's the environment variable Jwt__SigningKey (a double
> underscore stands for `:`), set by the host and often loaded from a vault like Key Vault (rotation, access
> control, an audit log); a file on the server with tight permissions is another option. Never in git,
> appsettings.json or the image.
>
> **The options chain.** `AddOptions<JwtOptions>()` registers it, so a constructor asking for IOptions<JwtOptions>
> gets a filled object. `.Bind(GetSection("Jwt"))` fills it from the Jwt section of the merged configuration, all
> layers: Issuer from appsettings.json, SigningKey from user secrets or the environment.
>
> **Validation.** `.ValidateDataAnnotations()` checks the attributes on JwtOptions against my configuration, not a
> client: [Required] (not null or empty, and the default is ""), [MinLength(32)] (HMAC-SHA256 needs a 256-bit key),
> [Range(1, 1440)] for LifetimeMinutes. `.ValidateOnStart()` runs that check at startup, at runtime: a missing key
> means the app refuses to start and the deploy fails at once. Without it the check is lazy, on the first
> options.Value: the app looks healthy, and the first login or authenticated request gets a 500. Better to find out
> from the deploy than from a user.
>
> **Why IOptions.** IOptions<JwtOptions> beats `configuration["Jwt:SigningKey"]` because it's typed (LifetimeMinutes
> is an int, not a string? to parse and null-check everywhere), validated once in one place, the section and
> property names are written once instead of as string literals where a typo silently returns null, and it's
> testable: `new TokenService( Options.Create(new JwtOptions { ... }))` needs no configuration at all. IOptions<T>
> is a singleton read once, so the singleton TokenService can take it safely; IOptionsSnapshot<T> (scoped, re-read
> per request) and IOptionsMonitor<T> (sees changes live) are its siblings.

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

**HMAC key vs public key (new in the 2026-11-24 quiz):** our dev tokens use HMAC, one shared secret that both signs and
verifies, so whoever can verify could also forge. Maskinporten signs with its **private** key, which only Maskinporten
has, and publishes the matching **public** key at a URL (JWKS). The API verifies with the public key and can't create
tokens. In code: `Authority = "https://maskinporten.no/"` (or the test environment) instead of `IssuerSigningKey`;
.NET downloads and refreshes the public keys itself. The issuer changes from `company-registry-dev` to Maskinporten.

**In repo:** `Auth/Scopes.cs` (`RequireScope`), `Program.cs` (policies + fallback policy), `Auth/DevTokenIssuer.cs`.

**Common question:** *"Why `MapInboundClaims = false`?"* → By default .NET renames JWT claims to long legacy URIs
(`sub` → `http://schemas.xmlsoap.org/.../nameidentifier`). Turning it off keeps claim names as they are in the token.

**In my words:**
> **401 vs 403.** Authentication is who you are: the JWT bearer handler validates the token (signature, iss, aud,
> exp) and sets HttpContext.User with its claims; it never rejects anything itself, a missing or invalid token just
> leaves the user anonymous. Authorisation never looks at the token, only at the user's claims: anonymous on a
> protected endpoint gives 401 (officially "Unauthorized", but it means unauthenticated). Authorisation is what you may
> do: a policy checks the claims, and a known caller without the right scope gets 403. From the tests: GET
> /api/companies without a token is 401; the reader (only companies.read) calling POST /api/imports is 403. Laravel:
> auth middleware vs Gates/`can:`.
>
> **The token.** Practice2's JWT carries iss, aud, exp, sub (the user id; client_id is the machine-client claim in
> Maskinporten-style tokens, as in the first project), name (the username) and one scope claim per scope. It's
> header.payload.signature, where header and payload are base64, not encrypted: anyone can read them, so no secrets
> in a JWT. The signature is HMAC-SHA256 over "header.payload" with the signing key.
>
> **Why it's trusted.** The API trusts a token without a database lookup by recomputing that HMAC with its own key
> and comparing it with the signature: a match means it was made by someone holding the key and nothing changed
> since; then it checks iss, aud and exp. If the payload is changed (say a scope added) or the signature is, they
> don't match: 401, as in the tampered-signature test. The trade-off of stateless tokens: one can't be revoked
> before it expires without adding state back (a deny-list, short tokens + refresh).
>
> **Fallback policy.** The fallback policy, RequireAuthenticatedUser, applies to every endpoint that has no auth
> attribute of its own: it's the safety net, deny by default, so a new controller someone forgets to mark is
> protected instead of silently public.
>
> **Policies and [AllowAnonymous].** The policies companies.read and companies.import require a scope claim with
> exactly that value; [Authorize(Policy = ...)] on a class covers all its actions (Companies and Roles need read,
> Imports needs import). [AllowAnonymous] is the deliberate opening: on AuthController (you can't need a token to
> get one) and on GET /api/companies/{orgnr}, the public company page, while /roles in the same class still needs
> companies.read. Maskinporten works the same way: it issues the token with the scopes, and only the AddJwtBearer
> setup would change.

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
> **Templates.** `logger.LogInformation("Import run {RunId} started", runId)` keeps RunId as a named field next to
> the message, so Seq or Application Insights can filter (RunId = 15), group, count and average on it, and every
> "started" line shares one template, so they group as one event type. `$"Import run {runId} started"` makes a
> finished string: the field is lost and only text or regex search is left. It's also built before the call even
> when that level is off, meaning the line's level is below its category's minimum (a LogDebug while the minimum is
> Information): C# builds the string as an argument, then the logger drops it. A template lets the logger check the
> level first and format only if the line is written; in a loop over 100,000 companies that's 100,000 strings saved.
> JSON is just one way to print the fields.
>
> **Category.** The T in ILogger<ImportWorker> sets the category, the class's full name
> Practice2.Api.Import.ImportWorker: it's shown on each line, so I know where it came from, and it's the name the
> appsettings keys match against: Logging > LogLevel > "<category or prefix>": "<level>", e.g.
> "Practice2.Api.Import.ImportWorker": "Debug".
>
> **Levels.** The levels from low to high: Trace < Debug < Information < Warning < Error < Critical, and a setting
> is the minimum written. "Default": "Information" covers every category without a more specific rule.
> "Microsoft.AspNetCore": "Warning" applies to categories starting with that name (prefix match, the longest wins)
> and hides the framework's per-request Information lines; my own request log line replaces them. In Development,
> "Microsoft.EntityFrameworkCore.Database.Command": "Information" logs every SQL statement EF sends.
>
> **Without a redeploy.** To see Debug in production without a redeploy, it's configuration: set
> Logging__LogLevel__Default=Debug (or narrower, Logging__LogLevel__Practice2.Api.Import=Debug) and restart, or edit
> appsettings.json on disk, which is watched and reloaded live.
>
> **Request scope.** CorrelationIdMiddleware is first in the pipeline. It opens `using var scope =
> logger.BeginScope("CorrelationId {CorrelationId}", id)` and then calls `await next(context)`, the rest of the
> pipeline. A scope belongs to the async flow (an AsyncLocal), not to one logger, so every logger writing inside it
> adds the id as a field to its lines: the controller, EF, the auth handler, and the middleware's own finally line.
>
> **Worker scope.** ImportWorker isn't in that flow: its ExecuteAsync started at app startup, and by the time it
> picks up a run id the request has ended (202) and its scope is closed; the Channel carries only the id. So 12b
> stores the id on the import_runs row, and the worker opens its own scope from run.CorrelationId, covering the
> importers, EF, HttpClient and Polly for that run. Scopes nest, adding fields to the outer one; they don't replace
> it.

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
> **Fact vs Theory.** [Fact] is one test with no parameters that runs once; [Theory] + [InlineData] runs the same
> test once per row, and each row is reported as its own test, for when only the data changes (PHPUnit's
> dataProvider, Jest's test.each). That's independent of unit vs integration: OrgnrTests has both
> (Rejects_invalid_values is a Theory, the check-digit-10 prefix is a Fact), and so does AuthorizationTests (the 401
> Theory over four URLs).
>
> **Unit vs integration.** OrgnrTests is a unit test: it calls Orgnr.IsValid directly, no app, no HTTP, no database,
> in milliseconds, so it covers every MOD11 edge case (length, null, "", non-digits, the check digit), but it can't
> prove an endpoint uses the rule. AuthorizationTests is an integration test: real HTTP through the whole app,
> proving the wiring no unit test can (middleware order, JWT validation, policies, the fallback policy,
> [AllowAnonymous]); it doesn't touch orgnr. That an endpoint uses MOD11 is proved once by RolesTests (123456789 →
> 400). Edge cases in unit tests, one integration test per behaviour: the test pyramid.
>
> **WebApplicationFactory.** WebApplicationFactory<Program> runs my real Program.cs (DI, middleware, auth, EF) in
> memory without a port; CreateClient() gives an HttpClient that talks to it directly, like a Laravel feature test's
> getJson. Before it starts I can override configuration (UseSetting) and services (ConfigureTestServices).
>
> **Real Postgres.** Testcontainers gives each factory a throwaway postgres:17-alpine, so queries, the migrations
> themselves, constraints, unique indexes, transactions and snake_case names run as in production. EF's InMemory
> provider isn't relational (no SQL, no constraints), and SQLite is a different engine (no ILIKE, different types).
>
> **Migrate and seed first.** ApiFactory migrates before the app starts because ImportWorker starts with the app and
> immediately runs MarkUnfinishedRunsAbandonedAsync, an UPDATE on import_runs; without the table it fails. EF itself
> doesn't check the schema at startup, only when a query hits a missing table. It seeds first because tests need
> known data: admin and reader with hashed passwords to log in, and company 974760673 with three unsorted roles, so
> a test can assert DAGL, MEDL, REVI.
>
> **Collections.** Each [CollectionDefinition] with ICollectionFixture<ApiFactory> gets its own ApiFactory: its own
> app, container and database. Tests inside a collection run one after another; collections run in parallel. The
> import writes: the role import replaces the roles of every company in its database, so in the shared "api"
> database it would wipe the seeded roles, and Company_roles_are_sorted_by_rank would fail only when the import ran
> first. Rule: read-only tests share a database; a test that changes shared data gets its own.
>
> **Flaky.** A flaky test is one that sometimes passes and sometimes fails on the same code, because of timing, test
> order or an outside system; people learn to ignore red.
>
> **Fake Brreg.** ImportTests fakes Brreg because a test must be fast, repeatable and offline, and the real Brreg
> takes over a minute, needs the network, can be down or rate-limit, and its data changes daily, so Inserted = 1,
> Rejected = 1, RolesSaved = 1 could never be asserted. Everything above the handler stays real.

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