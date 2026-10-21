namespace CompanyRegistry.Api.Data;

public class Company
{
    public required string OrganisationNumber { get; set; }
    public string Name { get; set; } = "";
    public string OrganisationFormCode { get; set; } = "";
    public string? OrganisationFormDescription { get; set; }
    public string? StreetAddress { get; set; }
    public string? PostalCode { get; set; }
    public string? PostalPlace { get; set; }
    public string? MunicipalityNumber { get; set; }
    public string? Municipality { get; set; }
    public DateOnly? RegistrationDate { get; set; }

    public DateTimeOffset FirstImportedAt { get; set; }
    public DateTimeOffset LastChangedAt { get; set; }
}
