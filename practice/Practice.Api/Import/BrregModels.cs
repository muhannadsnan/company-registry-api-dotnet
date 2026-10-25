using System.Text.Json.Serialization;

namespace Practice.Api.Import;

public record BrregPage(
    [property: JsonPropertyName("_embedded")] BrregEmbedded? Embedded,
    BrregPageInfo Page
);

public record BrregEmbedded(IReadOnlyList<BrregEnhet> Enheter);

public record BrregPageInfo(int Size, int TotalElements, int TotalPages, int Number);

public record BrregEnhet(
    string? Organisasjonsnummer,
    string? Navn,
    BrregOrgForm? Organisasjonsform,
    DateOnly? RegistreringsdatoEnhetsregisteret  
);

public record BrregOrgForm(string? Kode);