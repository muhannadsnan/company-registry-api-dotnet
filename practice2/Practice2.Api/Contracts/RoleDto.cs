using Practice2.Api.Data;
using Practice2.Api.Domain;

namespace Practice2.Api.Contracts;

public record RoleDto( string RoleType, string RoleName, string? PersonName, int? BirthYear, 
    string? UnitOrgnr, string? UnitName, bool Resigned)
{
    public static RoleDto Fill(Role r) => new(
        r.RoleType, RoleTypes.NameOf(r.RoleType),
        r.PersonName, r.BirthYear, r.UnitOrgnr, r.UnitName, r.Resigned);
};


public record CompanyDetailsDto(string Orgnr, string Name, string OrgForm, DateOnly? RegistrationDate,
    List<RoleDto> Roles)
{
    public static CompanyDetailsDto Fill(Company c) => new(
        c.Orgnr, c.Name, c.OrgForm, c.RegistrationDate,
        c.Roles!.Select(r => RoleDto.Fill(r)).ToList());

    // Select(r => RoleDto.Fill(r)): or simply pass the method itself: Select(RoleDto.Fill)

}