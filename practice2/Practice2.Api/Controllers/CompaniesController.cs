using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Practice2.Api.Contracts;
using Practice2.Api.Data;

namespace Practice2.Api.Controllers;

[ApiController]
[Route("api/companies")]
public class CompaniesController(AppDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<List<CompanyDto>> Search(CancellationToken ct) =>
        await db.Companies.AsNoTracking()
                .OrderBy(c => c.Name)
                .Take(20)
                .Select(c => new CompanyDto(c.Orgnr, c.Name, c.OrgForm, c.RegistrationDate))
                .ToListAsync(ct);

    [HttpGet("{orgnr}")]
    public async Task<ActionResult<CompanyDto>> GetByOrgnr(string orgnr, CancellationToken ct)
    {
        var company = await db.Companies.AsNoTracking()
                .Where(c => c.Orgnr == orgnr)
                .Select(c => new CompanyDto(c.Orgnr, c.Name, c.OrgForm, c.RegistrationDate))
                .FirstOrDefaultAsync(ct);
        return company is null ? NotFound() : company;
    }
}