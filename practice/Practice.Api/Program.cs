using Microsoft.EntityFrameworkCore;
using Practice.Api.Data;
using Practice.Api.Import;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.

builder.Services.AddControllers();
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

// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseHttpsRedirection();

app.UseAuthorization();

app.MapControllers();

app.Run();
