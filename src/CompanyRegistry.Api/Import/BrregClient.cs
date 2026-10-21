using Microsoft.Extensions.Options;

namespace CompanyRegistry.Api.Import;

/// <summary>
/// Typed HTTP client for Enhetsregisteret. The HttpClient is created and configured by IHttpClientFactory
/// (base address, retries, timeouts) in Program.cs; this class only knows how to ask for a page.
/// </summary>
public sealed class BrregClient(HttpClient http, IOptions<ImportOptions> options)
{
    public async Task<BrregPage> GetPageAsync(int page, CancellationToken ct)
    {
        var o = options.Value;
        var query = new Dictionary<string, string?>
        {
            ["page"] = page.ToString(),
            ["size"] = o.PageSize.ToString(),
            ["kommunenummer"] = o.MunicipalityNumber,
            ["organisasjonsform"] = o.OrganisationForm,
        };
        var url = "enhetsregisteret/api/enheter?" + string.Join('&', query
            .Where(kv => !string.IsNullOrEmpty(kv.Value))
            .Select(kv => $"{kv.Key}={Uri.EscapeDataString(kv.Value!)}"));

        return await http.GetFromJsonAsync<BrregPage>(url, ct)
            ?? throw new InvalidOperationException($"Brreg returned an empty body for page {page}.");
    }
}
