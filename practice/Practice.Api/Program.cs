using System.Text.Json.Serialization;
using Microsoft.EntityFrameworkCore;
using Practice.Api.Data;
using Practice.Api.Import;
using Practice.Api.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers()
    .AddJsonOptions(o => o.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddAuthentication().AddJwtBearer();
builder.Services.AddAuthorizationBuilder()
    .AddPolicy("companies.read", p => p.RequireClaim("scope", "companies.read"))
    .AddPolicy("companies.import", p => p.RequireClaim("scope", "companies.import"));
builder.Services.AddDbContext<AppDbContext>(options => options
    .UseNpgsql(builder.Configuration.GetConnectionString("Practice"))
    .UseSnakeCaseNamingConvention()
);
builder.Services.AddHttpClient<BrregClient>(http => 
{
    http.BaseAddress = new Uri("https://data.brreg.no/");
    http.DefaultRequestHeaders.UserAgent.ParseAdd("practice-api/1.0");
})
.AddStandardResilienceHandler();
builder.Services.AddScoped<CompanyImporter>();
builder.Services.AddSingleton<ImportQueue>();
builder.Services.AddHostedService<ImportWorker>();

// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

var app = builder.Build();
app.UseMiddleware<CorrelationIdMiddleware>();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.UseAuthentication();
app.UseAuthorization();

app.MapControllers();

app.Run();
