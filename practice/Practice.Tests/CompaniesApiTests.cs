using System.Net;
using System.Net.Http.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Practice.Api.Contracts;
using Practice.Api.Data;

namespace Practice.Tests;

[Collection("api")]
public class CompaniesApiTests(ApiFactory api) : IAsyncLifetime
{
    public async Task InitializeAsync()
    {
        using var scope = api.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Companies.ExecuteDeleteAsync();
        db.Companies.AddRange(
            new Company { Orgnr = "923609016", Name = "EQUINOR ASA", OrgForm = "ASA" },
            new Company { Orgnr = "912855414", Name = "ALT I BILDELER AS", OrgForm = "AS" },
            new Company { Orgnr = "974760673", Name = "BRØNNØYSUNDREGISTRENE", OrgForm = "ORGL" });
        await db.SaveChangesAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Request_without_token_is_401()
    {
        var response = await api.CreateClient().GetAsync("/api/companies");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Reader_cannot_start_an_import()
    {
        var reader = api.CreateClientWithScopes("companies.read");
        var response = await reader.PostAsync("/api/imports", null);
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task Search_filters_by_part_of_the_name()
    {
        var reader = api.CreateClientWithScopes("companies.read");

        var result = await reader.GetFromJsonAsync<PageResult<CompanyDto>>("/api/companies?name=bil");

        var company = Assert.Single(result!.Items);
        Assert.Equal("912855414", company.Orgnr);
        Assert.Equal(1, result.TotalCount);
    }

    [Fact]
    public async Task Invalid_orgnr_is_400()
    {
        var reader = api.CreateClientWithScopes("companies.read");
        var response = await reader.GetAsync("/api/companies/123456789");
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Unknown_but_valid_orgnr_is_404()
    {
        var reader = api.CreateClientWithScopes("companies.read");
        var response = await reader.GetAsync("/api/companies/987654325");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
