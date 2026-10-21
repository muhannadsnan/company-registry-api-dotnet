using System.Net;
using System.Net.Http.Json;
using CompanyRegistry.Api.Contracts;
using CompanyRegistry.Api.Data;
using CompanyRegistry.Tests.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CompanyRegistry.Tests;

[Collection("api")]
public class CompaniesApiTests(ApiFactory api) : IAsyncLifetime
{
    public async Task InitializeAsync()
    {
        await using var scope = api.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await db.Companies.Where(c => c.Name.StartsWith("TESTSEED")).ExecuteDeleteAsync();
        db.Companies.AddRange(
            Seed("974760673", "TESTSEED Fjordbåt AS", "AS"),
            Seed("912855414", "TESTSEED Fjellbil AS", "AS"),
            Seed("927386380", "TESTSEED Fjordkafé ENK", "ENK"));
        await db.SaveChangesAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;

    [Fact]
    public async Task Search_matches_part_of_the_name_case_insensitively_and_filters_by_form()
    {
        var client = await api.CreateClientAsAsync("reader");

        var result = await client.GetFromJsonAsync<PagedResult<CompanyDto>>("/api/companies?name=testseed fjord&organisationForm=as");

        var company = Assert.Single(result!.Items);
        Assert.Equal("974760673", company.OrganisationNumber);
        Assert.Equal(1, result.TotalCount);
    }

    [Fact]
    public async Task Search_treats_percent_as_a_literal_character()
    {
        var client = await api.CreateClientAsAsync("reader");

        var result = await client.GetFromJsonAsync<PagedResult<CompanyDto>>("/api/companies?name=testseed%25");

        Assert.Empty(result!.Items);
    }

    [Fact]
    public async Task Get_by_organisation_number_returns_the_company()
    {
        var client = await api.CreateClientAsAsync("reader");

        var company = await client.GetFromJsonAsync<CompanyDto>("/api/companies/912855414");

        Assert.Equal("TESTSEED Fjellbil AS", company!.Name);
        Assert.Equal("3905", company.Address.MunicipalityNumber);
    }

    [Fact]
    public async Task Get_with_invalid_check_digit_is_400()
    {
        var client = await api.CreateClientAsAsync("reader");
        var response = await client.GetAsync("/api/companies/912855415");
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Get_unknown_but_valid_number_is_404()
    {
        var client = await api.CreateClientAsAsync("reader");
        var response = await client.GetAsync("/api/companies/987654325");
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Page_size_above_100_is_rejected()
    {
        var client = await api.CreateClientAsAsync("reader");
        var response = await client.GetAsync("/api/companies?pageSize=1000");
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    private static Company Seed(string number, string name, string form) => new()
    {
        OrganisationNumber = number,
        Name = name,
        OrganisationFormCode = form,
        MunicipalityNumber = "3905",
        Municipality = "TØNSBERG",
        FirstImportedAt = DateTimeOffset.UtcNow,
        LastChangedAt = DateTimeOffset.UtcNow,
    };
}
