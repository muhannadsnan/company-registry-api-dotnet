using System.Net;
using System.Net.Http.Json;
using CompanyRegistry.Api.Contracts;
using CompanyRegistry.Tests.Infrastructure;

namespace CompanyRegistry.Tests;

[Collection("api")]
public class ImportPipelineTests(ApiFactory api)
{
    [Fact]
    public async Task Import_rejects_invalid_rows_upserts_the_rest_and_is_safe_to_rerun()
    {
        var client = await api.CreateClientAsAsync("importer");
        client.DefaultRequestHeaders.Add("X-Correlation-ID", "import-test-1");

        api.Brreg.Units =
        [
            FakeBrreg.Unit("923609016", "IMPORTTEST Equinor ASA", "ASA"),
            FakeBrreg.Unit("916627939", "IMPORTTEST Palmera"),
            FakeBrreg.Unit("923609017", "IMPORTTEST Bad check digit"),
        ];

        var first = await RunImportAsync(client);

        Assert.Equal("Succeeded", first.Status);
        Assert.Equal("import-test-1", first.CorrelationId);
        Assert.Equal("importer", first.RequestedBy);
        Assert.Equal((3, 2, 0, 0, 1), (first.RowsRead, first.RowsInserted, first.RowsUpdated, first.RowsUnchanged, first.RowsRejected));
        Assert.Contains(api.Brreg.Requests, uri => uri.Query.Contains("kommunenummer=3905"));

        // Same data plus one renamed company: one update, one untouched row, nothing duplicated.
        api.Brreg.Units =
        [
            FakeBrreg.Unit("923609016", "IMPORTTEST Equinor ASA", "ASA"),
            FakeBrreg.Unit("916627939", "IMPORTTEST Palmera Galleri"),
        ];

        var second = await RunImportAsync(client);

        Assert.Equal((2, 0, 1, 1, 0), (second.RowsRead, second.RowsInserted, second.RowsUpdated, second.RowsUnchanged, second.RowsRejected));

        var renamed = await client.GetFromJsonAsync<CompanyDto>("/api/companies/916627939");
        Assert.Equal("IMPORTTEST Palmera Galleri", renamed!.Name);
        var rejected = await client.GetAsync("/api/companies/923609017");
        Assert.Equal(HttpStatusCode.BadRequest, rejected.StatusCode);
    }

    private static async Task<ImportRunDto> RunImportAsync(HttpClient client)
    {
        var response = await client.PostAsync("/api/imports", null);
        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);
        var location = response.Headers.Location!;

        // The import runs in a background service; poll the run until it reaches a final state.
        var deadline = DateTime.UtcNow.AddSeconds(30);
        while (DateTime.UtcNow < deadline)
        {
            var run = await client.GetFromJsonAsync<ImportRunDto>(location);
            if (run!.Status is not ("Queued" or "Running"))
            {
                return run;
            }
            await Task.Delay(100);
        }
        throw new TimeoutException("Import did not finish within 30 seconds.");
    }
}
