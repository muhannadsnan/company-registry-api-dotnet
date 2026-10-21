using System.Net.Http.Headers;
using System.Net.Http.Json;
using CompanyRegistry.Api.Import;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.PostgreSql;

namespace CompanyRegistry.Tests.Infrastructure;

/// <summary>
/// Runs the real API in memory against a throwaway Postgres in Docker, with Brreg replaced by <see cref="FakeBrreg"/>.
/// Shared by every test in the "api" collection, so the container starts once.
/// </summary>
public sealed class ApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer _db = new PostgreSqlBuilder("postgres:17-alpine").Build();

    public FakeBrreg Brreg { get; } = new();

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:Companies", _db.GetConnectionString());
        builder.UseSetting("Database:MigrateOnStartup", "true");
        builder.UseSetting("Auth:SigningKey", "test-signing-key-that-is-long-enough-0123456789");
        builder.UseSetting("Auth:EnableDevTokenEndpoint", "true");
        builder.UseSetting("Auth:Clients:0:ClientId", "reader");
        builder.UseSetting("Auth:Clients:0:ClientSecret", "reader-secret");
        builder.UseSetting("Auth:Clients:0:Scopes:0", "companies.read");
        builder.UseSetting("Auth:Clients:1:ClientId", "importer");
        builder.UseSetting("Auth:Clients:1:ClientSecret", "importer-secret");
        builder.UseSetting("Auth:Clients:1:Scopes:0", "companies.read");
        builder.UseSetting("Auth:Clients:1:Scopes:1", "companies.import");

        builder.ConfigureTestServices(services => services
            .AddHttpClient<BrregClient>()
            .ConfigurePrimaryHttpMessageHandler(() => new FakeBrregHandler(Brreg)));
    }

    /// <summary>An HttpClient that sends a bearer token for the given test client.</summary>
    public async Task<HttpClient> CreateClientAsAsync(string clientId)
    {
        var client = CreateClient();
        var response = await client.PostAsync("/connect/token", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "client_credentials",
            ["client_id"] = clientId,
            ["client_secret"] = $"{clientId}-secret",
        }));
        response.EnsureSuccessStatusCode();
        var token = await response.Content.ReadFromJsonAsync<TokenResponse>();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token!.access_token);
        return client;
    }

    public Task InitializeAsync() => _db.StartAsync();

    async Task IAsyncLifetime.DisposeAsync()
    {
        await base.DisposeAsync();
        await _db.DisposeAsync();
    }

    private sealed record TokenResponse(string access_token, string scope);
}

[CollectionDefinition("api")]
public sealed class ApiCollection : ICollectionFixture<ApiFactory>;
