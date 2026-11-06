using System.Net;

namespace Practice2.Api.Import;

public class BrregClient(HttpClient http)
{
    public async Task<BrregPage> GetPageAsync(int page, int size, CancellationToken ct)
    {
        // Horten (3901) and Tønsberg (3905), AS and ASA.
        var url = $"enhetsregisteret/api/enheter?page={page}&size={size}&kommunenummer=3901,3905&organisasjonsform=AS,ASA";

        return await http.GetFromJsonAsync<BrregPage>(url, ct)
            ?? throw new InvalidOperationException($"Brreg returned an empty body for page {page}.");
    }

    // Brreg nests the roles in groups; the importer only needs the roles themselves.
    public async Task<IReadOnlyList<BrregRole>> GetRolesAsync(string orgnr, CancellationToken ct)
    {
        var response = await http.GetAsync($"enhetsregisteret/api/enheter/{orgnr}/roller", ct);

        // A company deleted from Brreg since the company import: no roles, not an error.
        if (response.StatusCode == HttpStatusCode.NotFound)
            return [];

        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<BrregRoles>(ct);
        return result?.Rollegrupper?.SelectMany(g => g.Roller ?? []) .ToList() ?? []; // SelectMany() is like array_merge in PHP
    }
}