using System.Net;
using System.Net.Http.Headers;
using Practice2.Tests.Infrastructure;

namespace Practice2.Tests;

[Collection("api")]
public class AuthorizationTests(ApiFactory api)
{
    [Theory]
    [InlineData("/api/companies")]
    [InlineData("/api/companies/974760673/roles")]
    [InlineData("/api/roles")]
    [InlineData("/api/imports")]
    public async Task Without_a_token_protected_endpoints_return_401(string url)
    {
        var response = await api.CreateClient().GetAsync(url);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Company_details_are_public()
    {
        var response = await api.CreateClient().GetAsync($"/api/companies/{ApiFactory.CompanyOrgnr}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task Reader_can_read_but_not_import()
    {
        var reader = await api.CreateClientAsAsync("reader");

        Assert.Equal(HttpStatusCode.OK, (await reader.GetAsync("/api/roles")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await reader.GetAsync("/api/imports")).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await reader.PostAsync("/api/imports", null)).StatusCode);
    }

    [Fact]
    public async Task Admin_can_list_imports()
    {
        var admin = await api.CreateClientAsAsync("admin");

        var response = await admin.GetAsync("/api/imports");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task A_tampered_token_returns_401()
    {
        var client = await api.CreateClientAsAsync("reader");
        var parts = client.DefaultRequestHeaders.Authorization!.Parameter!.Split('.');
        parts[2] = (parts[2][0] == 'A' ? "B" : "A") + parts[2][1..];   // change the signature's first character
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", string.Join('.', parts));

        var response = await client.GetAsync("/api/roles");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
