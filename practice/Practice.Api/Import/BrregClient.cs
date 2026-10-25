namespace Practice.Api.Import;

public class BrregClient(HttpClient http)
{
    public async Task<BrregPage> GetPageAsync(int page, int size, CancellationToken ct)
    {
        // Horten (3901) and Tønsberg (3905), AS and ASA. Configurable later.
        var url = $"enhetsregisteret/api/enheter?page={page}&size={size}&kommunenummer=3901,3905&organisasjonsform=AS,ASA";

        return await http.GetFromJsonAsync<BrregPage>(url, ct)
            ?? throw new InvalidOperationException($"Brreg  returned an empty body for page {page}.");

    }
}