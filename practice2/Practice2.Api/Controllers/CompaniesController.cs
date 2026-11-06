using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Practice2.Api.Import;
using Practice2.Api.Contracts;
using Practice2.Api.Data;
using Practice2.Api.Domain;

namespace Practice2.Api.Controllers;

[ApiController]
[Route("api/companies")]
public class CompaniesController(AppDbContext db) : ControllerBase
{
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
    public async Task<ActionResult<CompanyDto>> GetByOrgnr(string orgnr, CancellationToken ct)
    {
        if (!Orgnr.IsValid(orgnr))
        {
            return Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Invalid ORGNR",
                detail: "An ORGNR should be nine digits with a valid MOD11 check digit."
            );
        }
        var company = await db.Companies.AsNoTracking()
                .Where(c => c.Orgnr == orgnr)
                .Select(c => new CompanyDto(c.Orgnr, c.Name, c.OrgForm, c.RegistrationDate))
                .FirstOrDefaultAsync(ct);
        return company is null ? NotFound() : company;
    }

    [HttpGet("{orgnr}/roles")]
    public async Task<ActionResult<List<RoleDto>>> GetRoles(string orgnr, CancellationToken ct)
    {
        if (!Orgnr.IsValid(orgnr))
        {
            return Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Invalid ORGNR",
                detail: "An ORGNR should be 9 digits with a valid MOD11 check digit."
            );
        }

        // Include: load the company AND its roles, in one query (a LEFT JOIN).
        var company = await db.Companies.AsNoTracking()
            .Include(c => c.Roles)
            .FirstOrDefaultAsync(c => c.Orgnr == orgnr, ct);
        if (company is null)
            return NotFound();


        return company.Roles!
            .Select(r => new RoleDto(
                r.RoleType, RoleTypes.TextOf(r.RoleType),
                r.PersonName, r.BirthYear, r.UnitOrgnr, r.UnitName, r.Resigned))
            .OrderBy(r => r.RoleName)
            .ThenBy(r => r.PersonName ?? r.UnitName)
            .ToList();
    }

}