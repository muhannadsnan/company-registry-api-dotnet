using CompanyRegistry.Api.Auth;
using CompanyRegistry.Api.Contracts;
using CompanyRegistry.Api.Data;
using CompanyRegistry.Api.Import;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CompanyRegistry.Api.Controllers;

[ApiController]
[Route("api/imports")]
[Authorize(Policy = Scopes.CompaniesImport)]
public sealed class ImportsController(AppDbContext db, ImportRunService importRuns) : ControllerBase
{
    /// <summary>Queue an import from Brreg. Returns 202 at once; poll the Location to follow progress.</summary>
    [HttpPost]
    [ProducesResponseType<ImportRunDto>(StatusCodes.Status202Accepted)]
    [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Start(CancellationToken ct)
    {
        var clientId = User.FindFirst("client_id")?.Value;
        var result = await importRuns.QueueAsync("api", clientId, HttpContext.TraceIdentifier, ct);

        if (result.AlreadyActive)
        {
            return Problem(
                statusCode: StatusCodes.Status409Conflict,
                title: "An import is already in progress",
                detail: $"Import run {result.Run.Id} is {result.Run.Status}.",
                instance: Url.Action(nameof(Get), new { id = result.Run.Id }));
        }

        return AcceptedAtAction(nameof(Get), new { id = result.Run.Id }, ImportRunDto.From(result.Run));
    }

    /// <summary>The most recent import runs, newest first.</summary>
    [HttpGet]
    public async Task<IReadOnlyList<ImportRunDto>> List(CancellationToken ct)
    {
        var runs = await db.ImportRuns.AsNoTracking().OrderByDescending(r => r.Id).Take(20).ToListAsync(ct);
        return runs.Select(ImportRunDto.From).ToList();
    }

    [HttpGet("{id:long}")]
    public async Task<ActionResult<ImportRunDto>> Get(long id, CancellationToken ct)
    {
        var run = await db.ImportRuns.AsNoTracking().FirstOrDefaultAsync(r => r.Id == id, ct);
        return run is null ? NotFound() : ImportRunDto.From(run);
    }
}
