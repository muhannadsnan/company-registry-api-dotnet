using CompanyRegistry.Api.Auth;
using CompanyRegistry.Api.Contracts;
using CompanyRegistry.Api.Data;
using CompanyRegistry.Api.Domain;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CompanyRegistry.Api.Controllers;

[ApiController]
[Route("api/companies")]
[Authorize(Policy = Scopes.CompaniesRead)]
public sealed class CompaniesController(AppDbContext db) : ControllerBase
{
    /// <summary>Search companies. All filters are optional and combine with AND.</summary>
    [HttpGet]
    public async Task<ActionResult<PagedResult<CompanyDto>>> Search([FromQuery] CompanySearchQuery query, CancellationToken ct)
    {
        // Nothing runs against the database while the query is being built up;
        // CountAsync and ToListAsync below are what send SQL.
        IQueryable<Company> companies = db.Companies.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(query.Name))
        {
            companies = companies.Where(c => EF.Functions.ILike(c.Name, $"%{EscapeLike(query.Name.Trim())}%", @"\"));
        }
        if (!string.IsNullOrWhiteSpace(query.OrganisationForm))
        {
            var form = query.OrganisationForm.Trim().ToUpperInvariant();
            companies = companies.Where(c => c.OrganisationFormCode == form);
        }
        if (!string.IsNullOrWhiteSpace(query.PostalCode))
        {
            companies = companies.Where(c => c.PostalCode == query.PostalCode);
        }
        if (query.RegisteredFrom is { } from)
        {
            companies = companies.Where(c => c.RegistrationDate >= from);
        }
        if (query.RegisteredTo is { } to)
        {
            companies = companies.Where(c => c.RegistrationDate <= to);
        }

        var total = await companies.CountAsync(ct);
        var items = await companies
            .OrderBy(c => c.Name)
            .ThenBy(c => c.OrganisationNumber)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(CompanyDto.FromEntity)
            .ToListAsync(ct);

        return new PagedResult<CompanyDto>(items, query.Page, query.PageSize, total);
    }

    /// <summary>Get one company by its nine-digit organisation number.</summary>
    [HttpGet("{organisationNumber}")]
    public async Task<ActionResult<CompanyDto>> GetByOrganisationNumber(string organisationNumber, CancellationToken ct)
    {
        if (!OrganisationNumber.IsValid(organisationNumber))
        {
            return Problem(
                statusCode: StatusCodes.Status400BadRequest,
                title: "Invalid organisation number",
                detail: "An organisation number is nine digits with a valid MOD11 check digit.");
        }

        var company = await db.Companies
            .AsNoTracking()
            .Where(c => c.OrganisationNumber == organisationNumber)
            .Select(CompanyDto.FromEntity)
            .FirstOrDefaultAsync(ct);

        return company is null ? NotFound() : company;
    }

    private static string EscapeLike(string value) =>
        value.Replace(@"\", @"\\").Replace("%", @"\%").Replace("_", @"\_");
}
