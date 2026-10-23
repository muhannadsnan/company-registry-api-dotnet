using Microsoft.AspNetCore.Mvc;

namespace Practice.Api.Controllers;

public record CompanyDto(string Orgnr, string Name, string Form);

[ApiController]
[Route("api/companies")]
public class CompaniesController : ControllerBase
{
    private static readonly List<CompanyDto> Companies = [
        new ("923609016", "Equinor ASA", "ASA"),
        new ("912855414", "Alt I Bilder", "AS"),
        new ("974760673", "BrønnøysundRegistrene", "ORGL"),
    ];

    [HttpGet]
    public IEnumerable<CompanyDto> GetAll() => Companies;

    [HttpGet("{orgnr}")]
    public ActionResult<CompanyDto> GetByOrgnr(string orgnr)
    {
        var company = Companies.FirstOrDefault(c => c.Orgnr == orgnr);
        if (company is null)
        {
            return NotFound();
        }
        return company;
    }
}