namespace Practice2.Api.Contracts;

public record RoleDto(
    string RoleType,
    string RoleName,
    string? PersonName,
    int? BirthYear,
    string? UnitOrgnr,
    string? UnitName,
    bool Resigned);