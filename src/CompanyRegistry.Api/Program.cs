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
    // "nonfile" skips paths with a file extension, so /assets/*.js still reach the static files;
    // the regex leaves unknown /api and /connect paths alone.
    app.MapFallbackToFile("{*path:nonfile:regex(^(?!api/|connect/).*$)}", "index.html").AllowAnonymous();
}

if (app.Configuration.GetValue<bool>("Database:MigrateOnStartup"))
{
    await using var scope = app.Services.CreateAsyncScope();
    await scope.ServiceProvider.GetRequiredService<AppDbContext>().Database.MigrateAsync();
}

app.Run();
