using System.Text.Json.Serialization;

namespace Practice2.Api.Brreg;

public record BrregEnhet(
    string? Organisasjonsnummer,
    string? Navn,
    BrregOrgForm? Organisasjonsform,
    DateOnly? RegistreringsdatoEnhetsregisteret
);

public record BrregOrgForm (string? Kode);



public record BrregEmbedded(IReadOnlyList<BrregEnhet> Enheter);

public record BrregPageInfo(int Size, int TotalElements, int TotalPages, int Number);

public record BrregPage(
    [property: JsonPropertyName("_embedded")] BrregEmbedded? Embedded,
    BrregPageInfo Page
);