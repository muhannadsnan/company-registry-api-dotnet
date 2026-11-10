using System.Net;
using System.Net.Http.Json;
using Practice2.Api.Contracts;
using Practice2.Api.Import;
using Practice2.Tests.Infrastructure;

namespace Practice2.Tests;

[Collection("import")]
public class ImportTests(ApiFactory api)
{
    [Fact]
    public async Task Import_saves_companies_and_roles_from_brreg()
    {
        // Arrange: what Brreg "has"
        api.Brreg.Companies.Add(new BrregEnhet("923609016", "Fjordselskap AS", new BrregOrgForm("AS"), new DateOnly(2020, 1, 15)));
        api.Brreg.Companies.Add(new BrregEnhet("123456789", "Ugyldig AS", new BrregOrgForm("AS"), null)); // bad MOD11
        api.Brreg.Roles["923609016"] =
        [
            new BrregRole(new BrregRoleType("DAGL"),
                new BrregPerson(new BrregPersonName("Ola", null, "Nordmann"), new DateOnly(1975, 5, 1)),
                null, null, false)
        ];
        var admin = await api.CreateClientAsAsync("admin");

        // Act: start the import
        var start = await admin.PostAsync("/api/imports", null);
        Assert.Equal(HttpStatusCode.Accepted, start.StatusCode);
        var run = await start.Content.ReadFromJsonAsync<ImportRunDto>();

        // The worker runs in the background: ask until the run is finished (max ~5 s).
        ImportRunDto? done = null;
        for (var i = 0; i < 50; i++)
        {
            done = await admin.GetFromJsonAsync<ImportRunDto>($"/api/imports/{run!.Id}");
            if (done!.Status is "Succeeded" or "Failed")
                break;
            await Task.Delay(100);
        }

        // Assert: the run's numbers, then the saved roles
        Assert.Equal("Succeeded", done!.Status);
        Assert.Equal(2, done.Read);
        Assert.Equal(1, done.Inserted);
        Assert.Equal(1, done.Rejected);
        Assert.Equal(1, done.RolesSaved);

        var roles = await admin.GetFromJsonAsync<List<RoleDto>>("/api/companies/923609016/roles");
        var role = Assert.Single(roles!);
        Assert.Equal("Ola Nordmann", role.PersonName);
        Assert.Equal(1975, role.BirthYear);
    }
}
