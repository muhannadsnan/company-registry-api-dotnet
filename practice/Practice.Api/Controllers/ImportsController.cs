using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Practice.Api.Import;
using Practice.Api.Data;
using System.Net;

namespace Practice.Api.Controllers;

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

        return AcceptedAtAction(nameof(Get), new {id = run.Id}, run);
    }

    [HttpGet("{id:long}")]
    public async Task<ActionResult<ImportRun>> Get(long id, CancellationToken ct)
    {
        var run = await db.ImportRuns.AsNoTracking().FirstOrDefaultAsync(r => r.Id == id, ct);
        return run is null ? NotFound() : run;
    }

    [HttpGet]
    public async Task<List<ImportRun>> List(CancellationToken ct) => 
        await db.ImportRuns.AsNoTracking().OrderByDescending(r => r.Id).Take(20).ToListAsync(ct);
}