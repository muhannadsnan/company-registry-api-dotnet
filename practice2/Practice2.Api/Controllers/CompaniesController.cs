using Microsoft.AspNetCore.Mvc;
using Practice2.Api.Contracts;

namespace Practice2.Api.Controllers;

[ApiController]
[Route("api/companies")]
public class CompaniesController : ControllerBase
{
    private static readonly List<CompanyDto> Companies =
    [
        new("923609016", "EQUINOR ASA", "ASA"),
        new("912855414", "ALT I BILDELER AS", "AS"),
    ];

    [HttpGet]
    public List<CompanyDto> Search() => Companies;

    [HttpGet("{orgnr}")]
    public ActionResult<CompanyDto> GetByOrgnr(string orgnr)
    {
        var company = Companies.FirstOrDefault(c => c.Orgnr == orgnr);
        return company is null ? NotFound() : company;
    }
}