using System.Net;
using System.Net.Http.Json;
using Practice2.Api.Contracts;
using Practice2.Tests.Infrastructure;

namespace Practice2.Tests;

[Collection("api")]
public class RolesTests(ApiFactory api)
{
    [Fact]
    public async Task Company_roles_are_sorted_by_rank()
    {
        var reader = await api.CreateClientAsAsync("reader");

        var roles = await reader.GetFromJsonAsync<List<RoleDto>>($"/api/companies/{ApiFactory.CompanyOrgnr}/roles");

        Assert.NotNull(roles);
        Assert.Equal(["DAGL", "MEDL", "REVI"], roles.Select(r => r.RoleType).ToArray());
        Assert.Equal("Daglig leder", roles[0].RoleName);
    }

    [Fact]
    public async Task Role_search_filters_by_role_type()
    {
        var reader = await api.CreateClientAsAsync("reader");

        var page = await reader.GetFromJsonAsync<PageResult<RoleSearchItemDto>>("/api/roles?roleType=dagl");

        Assert.Equal(1, page!.TotalCount);
        var role = Assert.Single(page.Items);
        Assert.Equal("Ola Nordmann", role.PersonName);
        Assert.Equal("Testselskap AS", role.CompanyName);
    }

    [Theory]
    [InlineData("123456789", HttpStatusCode.BadRequest)]   // wrong MOD11 check digit
    [InlineData("923609016", HttpStatusCode.NotFound)]     // valid, but not in the database
    public async Task Company_roles_for_a_bad_or_unknown_orgnr(string orgnr, HttpStatusCode expected)
    {
        var reader = await api.CreateClientAsAsync("reader");

        var response = await reader.GetAsync($"/api/companies/{orgnr}/roles");

        Assert.Equal(expected, response.StatusCode);
    }
}
