using Microsoft.EntityFrameworkCore;
using Practice2.Api.Data;
using Practice2.Api.Import;

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
builder.Services.AddSingleton<ImportQueue>();
builder.Services.AddHostedService<ImportWorker>();


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
