namespace Practice.Api.Data;

public class Company
{
    public required string Orgnr { get; set; }
    public required string Name { get; set; }
    public required string OrgForm { get; set; }
    public DateOnly? RegistrationDate { get; set; }
}