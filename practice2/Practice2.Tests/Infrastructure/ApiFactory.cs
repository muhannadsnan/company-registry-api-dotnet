using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Practice2.Api.Contracts;
using Practice2.Api.Data;
using Testcontainers.PostgreSql;
using System.Net.Http.Headers;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Practice2.Api.Import;

namespace Practice2.Tests.Infrastructure;

// The real API in memory, against a throwaway Postgres in Docker.
public class ApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    public const string Password = "test-pass-123";
    public const string CompanyOrgnr = "974760673";
    public FakeBrreg Brreg { get; } = new();

    private readonly PostgreSqlContainer _db = new PostgreSqlBuilder("postgres:17-alpine").Build();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:Practice2", _db.GetConnectionString());
        builder.UseSetting("Jwt:SigningKey", "test-signing-key-that-is-long-enough-0123456789");
        builder.ConfigureTestServices(services => services
            .AddHttpClient<BrregClient>()
            .ConfigurePrimaryHttpMessageHandler(() => new FakeBrregHandler(Brreg)));
    }

    public async Task InitializeAsync()
    {
        await _db.StartAsync();

        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(_db.GetConnectionString())
            .UseSnakeCaseNamingConvention()
            .Options;
        await using var db = new AppDbContext(options);
        await db.Database.MigrateAsync();

        var hash = new PasswordHasher<User>().HashPassword(null!, Password); // the user argument isn't used
        db.Users.AddRange(
            new User { Username = "admin", PasswordHash = hash, Scopes = "companies.read companies.import" },
            new User { Username = "reader", PasswordHash = hash, Scopes = "companies.read" });
        db.Companies.Add(new Company
        {
            Orgnr = CompanyOrgnr, Name = "Testselskap AS", OrgForm = "AS",
            Roles =
            [   // deliberately unsorted
                new Role { Orgnr = CompanyOrgnr, RoleType = "MEDL", PersonName = "Kari Nordmann", BirthYear = 1980 },
                new Role { Orgnr = CompanyOrgnr, RoleType = "REVI", UnitOrgnr = "912855414", UnitName = "Revisor AS" },
                new Role { Orgnr = CompanyOrgnr, RoleType = "DAGL", PersonName = "Ola Nordmann", BirthYear = 1975 },
            ]
        });
        await db.SaveChangesAsync();
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        await base.DisposeAsync();
        await _db.DisposeAsync();
    }



    // logs in and returns a client that sends the token with every request
    public async Task<HttpClient> CreateClientAsAsync(string username)
    {
        var client = CreateClient();
        var response = await client.PostAsJsonAsync("/api/auth/login", new { username, password = Password });
        response.EnsureSuccessStatusCode();
        var login = await response.Content.ReadFromJsonAsync<LoginResponse>();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", login!.Token);
        return client;
    }
}

// Every class marked [Collection("api")] shares one ApiFactory, so one container.
[CollectionDefinition("api")]
public class ApiCollection : ICollectionFixture<ApiFactory>;

[CollectionDefinition("import")]
public class ImportCollection : ICollectionFixture<ApiFactory>;

