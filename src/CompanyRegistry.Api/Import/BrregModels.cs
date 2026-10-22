using System.Text.Json.Serialization;

namespace CompanyRegistry.Api.Import;

// Shapes of the Enhetsregisteret JSON we read. Property names match the API so no mapping attributes are needed.

public sealed record BrregPage(
    [property: JsonPropertyName("_embedded")] BrregEmbedded? Embedded,
    BrregPageInfo Page);

public sealed record BrregEmbedded(IReadOnlyList<BrregEnhet> Enheter);

public sealed record BrregPageInfo(int Size, int TotalElements, int TotalPages, int Number);

public sealed record BrregEnhet(
    string? Organisasjonsnummer,
    string? Navn,
    BrregKode? Organisasjonsform,
    BrregAdresse? Forretningsadresse,
    DateOnly? RegistreringsdatoEnhetsregisteret);

public sealed record BrregKode(string? Kode);

public sealed record BrregAdresse(
    IReadOnlyList<string>? Adresse,
    string? Postnummer,
    string? Poststed,
    string? Kommunenummer,
    string? Kommune);
