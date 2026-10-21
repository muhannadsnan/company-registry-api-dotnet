using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Json;

namespace CompanyRegistry.Tests.Infrastructure;

/// <summary>What the fake Enhetsregisteret returns. Tests set <see cref="Units"/> before triggering an import.</summary>
public sealed class FakeBrreg
{
    public IReadOnlyList<object> Units { get; set; } = [];
    public ConcurrentQueue<Uri> Requests { get; } = new();

    public static object Unit(string organisationNumber, string name, string form = "AS") => new
    {
        organisasjonsnummer = organisationNumber,
        navn = name,
        organisasjonsform = new { kode = form, beskrivelse = "Aksjeselskap" },
        forretningsadresse = new
        {
            adresse = new[] { "Storgaten 1" },
            postnummer = "3126",
            poststed = "TØNSBERG",
            kommunenummer = "3905",
            kommune = "TØNSBERG",
        },
        registreringsdatoEnhetsregisteret = "2020-01-15",
    };
}

public sealed class FakeBrregHandler(FakeBrreg brreg) : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
    {
        brreg.Requests.Enqueue(request.RequestUri!);
        var units = brreg.Units;
        var body = new
        {
            _embedded = new { enheter = units },
            page = new { size = 500, totalElements = units.Count, totalPages = 1, number = 0 },
        };
        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = JsonContent.Create(body) });
    }
}
