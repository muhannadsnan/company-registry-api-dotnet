namespace Practice2.Api.Contracts;

// One role, with the company it belongs to.
public record RoleSearchItemDto(
    string Orgnr,
    string CompanyName,
    string RoleType,
    string RoleName,
    string? PersonName,
    int? BirthYear,
    string? UnitOrgnr,
    string? UnitName,
    bool Resigned);