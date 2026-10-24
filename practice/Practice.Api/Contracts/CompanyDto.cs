namespace Practice.Api.Contracts;

public record CompanyDto(string Orgnr, string Name, string OrgForm, DateOnly? RegistrationDate);