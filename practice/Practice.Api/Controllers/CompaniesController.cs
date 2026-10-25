using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Practice.Api.Data;
using Practice.Api.Contracts;

namespace Practice.Api.Controllers;

// public record CompanyDto(string Orgnr, string Name, string Form);


[ApiController]
[Route("api/companies")]
// public class CompaniesController : ControllerBase
public class CompaniesController(AppDbContext db) : ControllerBase
{
    /* private static readonly List<CompanyDto> Companies = [
        new ("923609016", "Equinor ASA", "ASA"),
        new ("912855414", "Alt I Bilder", "AS"),
        new ("974760673", "BrønnøysundRegistrene", "ORGL"),
    ]; */

    /* [HttpGet]
    // public IEnumerable<CompanyDto> GetAll() => Companies;
    public async Task<List<CompanyDto>> GetAll(CancellationToken ct)
    {
        return await db.Companies
            .AsNoTracking()
            .OrderBy(c => c.Name)
            .Take(100)
            .Select(c => new CompanyDto(c.Orgnr, c.Name, c.OrgForm, c.RegistrationDate))
            .ToListAsync(ct);
    } */

    [HttpGet]
    public async Task<PageResult<CompanyDto>> Search([FromQuery] CompanySearchQuery query, CancellationToken ct)
    {
        IQueryable<Company> companies = db.Companies.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(query.Name))
        {
            companies = companies.Where(c => EF.Functions.ILike(c.Name, $"%{query.Name}%"));
        }
        if (!string.IsNullOrWhiteSpace(query.OrgForm))
        {
            var orgForm = query.OrgForm.Trim().ToUpperInvariant();
            companies = companies.Where(c => c.OrgForm == orgForm);
        }

        var total = await companies.CountAsync(ct);
        var items = await companies
            .OrderBy(c => c.Name)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(c => new CompanyDto(c.Orgnr, c.Name, c.OrgForm, c.RegistrationDate))
            .ToListAsync(ct);

        return new PageResult<CompanyDto>(items, query.Page, query.PageSize, total);
    }
    
    [HttpGet("{orgnr}")]
    // public ActionResult<CompanyDto> GetByOrgnr(string orgnr)
    public async Task<ActionResult<CompanyDto>> GetByOrgnr(string orgnr, CancellationToken ct)
    {
        // var company = Companies.FirstOrDefault(c => c.Orgnr == orgnr);
        var company = await db.Companies
            .AsNoTracking()
            .Where(c => c.Orgnr == orgnr)
            .Select(c => new CompanyDto(c.Orgnr, c.Name, c.OrgForm, c.RegistrationDate))
            .FirstOrDefaultAsync(ct);

        if (company is null)
        {
            return NotFound();
        }
        return company;
    }
}