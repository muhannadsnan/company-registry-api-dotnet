using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Practice2.Api.Contracts;
using Practice2.Api.Data;
using Practice2.Api.Domain;

namespace Practice2.Api.Controllers;

[ApiController]
[Route("api/roles")]
public class RolesController(AppDbContext db) : ControllerBase
{
    [HttpGet]
    public async Task<PageResult<RoleSearchItemDto>> Search([FromQuery] RolesSearchQuery query, CancellationToken ct)
    {
        IQueryable<Role> roles = db.Roles.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(query.Name))
        {
            // A person or a unit: one of the two names is NULL, and NULL ILIKE ... is never true.
            var keyword = $"%{query.Name.Trim()}%";
            roles = roles.Where(r => EF.Functions.ILike(r.PersonName!, keyword)
                                  || EF.Functions.ILike(r.UnitName!, keyword));
        }
        // Comma-separated: ?roleType=DAGL,LEDE  →  WHERE role_type = ANY(@roleTypes)
        var roleTypes = SplitList(query.RoleType).Select(t => t.ToUpperInvariant()).ToList();
        if (roleTypes.Count > 0)
        {
            roles = roles.Where(r => roleTypes.Contains(r.RoleType));
        }
        var orgnrs = SplitList(query.Orgnr);
        if (orgnrs.Count > 0)
        {
            roles = roles.Where(r => orgnrs.Contains(r.Orgnr));
        }
        
        if (query.BirthYearFrom is not null)
        {
            roles = roles.Where(r => r.BirthYear >= query.BirthYearFrom);
        }
        if (query.BirthYearTo is not null)
        {
            roles = roles.Where(r => r.BirthYear <= query.BirthYearTo);
        }

        var total = await roles.CountAsync(ct);
        var items = await roles
            .OrderBy(r => r.Company!.Name)
            .ThenBy(r => r.RoleType)
            .ThenBy(r => r.Id) // a unique last key: every page is stable
            .Skip((query.Page - 1) * (query.PageSize))
            .Take(query.PageSize)
            .Select(r => new RoleSearchItemDto(
                r.Company!.Orgnr, r.Company!.Name, r.RoleType, RoleTypes.NameOf(r.RoleType),
                r.PersonName, r.BirthYear, r.UnitOrgnr, r.UnitName, r.Resigned) )
            .ToListAsync(ct);

        return new PageResult<RoleSearchItemDto>(items, query.Page, query.PageSize, total);
    }

    //==============================================

    // "DAGL, lede,,MEDL" → ["DAGL", "lede", "MEDL"]; null or blank → []
    private static List<string> SplitList(string? value) =>
        string.IsNullOrWhiteSpace(value)
            ? []
            : value.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).ToList();

}