using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Practice2.Api.Data;
using Practice2.Api.Import;
using Practice2.Api.Contracts;
using Microsoft.AspNetCore.Authorization;

namespace Practice2.Api.Controllers;

[ApiController]
[Route("api/imports")]
[Authorize(Policy = "companies.import")]
public class ImportsController(AppDbContext db, ImportQueue queue) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Start(CancellationToken ct)
    {
        var active = await db.ImportRuns.AnyAsync(
            r => r.Status == ImportStatus.Queued || r.Status == ImportStatus.Running, ct);
        if (active)
            return Problem(statusCode: StatusCodes.Status409Conflict, title: "An import is already queued or running");

        var run = new ImportRun { Status = ImportStatus.Queued, QueuedAt = DateTimeOffset.UtcNow };
        db.ImportRuns.Add(run);
        await db.SaveChangesAsync(ct);
        await queue.EnqueueAsync(run.Id, ct);

        // respond with 202 here with Location header + dto, and Problem() returns 409 
        return Accepted($"/api/imports/{run.Id}", ImportRunDto.Fill(run));
    }

    [HttpGet("{id:long}")]
    public async Task<ActionResult<ImportRunDto>> Get(long id, CancellationToken ct)
    {
        var run = await db.ImportRuns.AsNoTracking().FirstOrDefaultAsync(r => r.Id == id, ct);
        return run is null ? NotFound() : ImportRunDto.Fill(run);
    }

    [HttpGet]
    public async Task<List<ImportRunDto>> List(CancellationToken ct)
    {
        var runs = await db.ImportRuns.AsNoTracking().OrderByDescending(r => r.Id).Take(20).ToListAsync(ct);
        return runs.Select(ImportRunDto.Fill).ToList();
    }
}