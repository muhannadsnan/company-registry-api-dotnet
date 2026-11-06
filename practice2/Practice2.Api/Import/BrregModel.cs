using System.Text.Json.Serialization;

namespace Practice2.Api.Import;

public record BrregEnhet(
    string? Organisasjonsnummer,
    string? Navn,
    BrregOrgForm? Organisasjonsform,
    DateOnly? RegistreringsdatoEnhetsregisteret
);

public record BrregOrgForm (string? Kode);
// ===
public record BrregEmbedded(IReadOnlyList<BrregEnhet> Enheter);

public record BrregPageInfo(int Size, int TotalElements, int TotalPages, int Number);

public record BrregPage(
    [property: JsonPropertyName("_embedded")] BrregEmbedded? Embedded,
    BrregPageInfo Page
);

//=========================  ROLES  ============================

// GET /enhetsregisteret/api/enheter/{orgnr}/roller
public record BrregRoles(IReadOnlyList<BrregRoleGroup>? Rollegrupper);

public record BrregRoleGroup(IReadOnlyList<BrregRole>? Roller);

public record BrregRole(BrregRoleType? Type, BrregPerson? Person, BrregRoleUnit? Enhet, BrregBostyrer? Bostyrer, bool Avregistrert);

public record BrregRoleType(string? Kode);

public record BrregPerson(BrregPersonName? Navn, DateOnly? Fodselsdato);

public record BrregPersonName(string? Fornavn, string? Mellomnavn, string? Etternavn);

public record BrregRoleUnit(string? Organisasjonsnummer, IReadOnlyList<string>? Navn);

public record BrregBostyrer(string? Navn);

