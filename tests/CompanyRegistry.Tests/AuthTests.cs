using System.Net;
using System.Net.Http.Json;
using CompanyRegistry.Tests.Infrastructure;

namespace CompanyRegistry.Tests;

[Collection("api")]
public class AuthTests(ApiFactory api)
{
    [Fact]
    public async Task Request_without_token_is_401()
    {
        var response = await api.CreateClient().GetAsync("/api/companies");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Token_with_only_read_scope_cannot_start_an_import()
    {
        var reader = await api.CreateClientAsAsync("reader");
        var response = await reader.PostAsync("/api/imports", null);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Wrong_client_secret_gets_no_token()
    {
        var response = await api.CreateClient().PostAsync("/connect/token", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "client_credentials",
            ["client_id"] = "reader",
            ["client_secret"] = "guess",
        }));
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Client_cannot_get_scopes_it_was_not_given()
    {
        var response = await api.CreateClient().PostAsync("/connect/token", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "client_credentials",
            ["client_id"] = "reader",
            ["client_secret"] = "reader-secret",
            ["scope"] = "companies.read companies.import",
        }));
        var body = await response.Content.ReadFromJsonAsync<Dictionary<string, object>>();
        Assert.Equal("companies.read", body!["scope"].ToString());
    }

    [Fact]
    public async Task Health_check_needs_no_token()
    {
        var response = await api.CreateClient().GetAsync("/health");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
