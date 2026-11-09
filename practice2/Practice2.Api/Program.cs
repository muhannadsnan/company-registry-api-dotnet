using Microsoft.EntityFrameworkCore;
using Practice2.Api.Data;
using Practice2.Api.Import;
using Practice2.Api.Auth;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Microsoft.AspNetCore.Authorization;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
builder.Services.AddDbContext<AppDbContext>(options => 
        options.UseNpgsql(builder.Configuration.GetConnectionString("Practice2"))
            .UseSnakeCaseNamingConvention()
    );
builder.Services.AddHttpClient<BrregClient>(http =>
    {
        http.BaseAddress = new Uri("https://data.brreg.no/");
        http.DefaultRequestHeaders.UserAgent.ParseAdd("practice2-api/1.0");
    })
    .AddStandardResilienceHandler();
builder.Services.AddScoped<CompanyImporter>();
builder.Services.AddScoped<RoleImporter>();
builder.Services.AddSingleton<ImportQueue>();
builder.Services.AddHostedService<ImportWorker>();

builder.Services.AddOptions<JwtOptions>()
    .Bind(builder.Configuration.GetSection(JwtOptions.Section))
    .ValidateDataAnnotations()
    .ValidateOnStart();
builder.Services.AddSingleton<TokenService>();    
builder.Services.AddSingleton<IPasswordHasher<User>, PasswordHasher<User>>();  

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
builder.Services.AddAuthorizationBuilder()
    .SetFallbackPolicy(new AuthorizationPolicyBuilder().RequireAuthenticatedUser().Build())
    .AddPolicy("companies.read", policy => policy.RequireClaim("scope", "companies.read"))
    .AddPolicy("companies.import", policy => policy.RequireClaim("scope", "companies.import"));


// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

var app = builder.Build();

app.UseHttpsRedirection();

app.UseAuthentication(); // both must come before MapControllers anyway
app.UseAuthorization();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
    app.MapOpenApi().AllowAnonymous();

app.MapControllers();

app.Run();
