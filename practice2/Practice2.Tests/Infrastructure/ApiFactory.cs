using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Practice2.Api.Data;
using Testcontainers.PostgreSql;

namespace Practice2.Tests.Infrastructure;

// The real API in memory, against a throwaway Postgres in Docker.
public class ApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    public const string Password = "test-pass-123";

    private readonly PostgreSqlContainer _db = new PostgreSqlBuilder("postgres:17-alpine").Build();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:Practice2", _db.GetConnectionString());
        builder.UseSetting("Jwt:SigningKey", "test-signing-key-that-is-long-enough-0123456789");
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
        await db.SaveChangesAsync();
    }

    async Task IAsyncLifetime.DisposeAsync()
    {
        await base.DisposeAsync();
        await _db.DisposeAsync();
    }
}

// Every class marked [Collection("api")] shares one ApiFactory, so one container.
[CollectionDefinition("api")]
public class ApiCollection : ICollectionFixture<ApiFactory>;
