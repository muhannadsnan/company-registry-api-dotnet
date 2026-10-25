using Microsoft.AspNetCore.Mvc;
using Practice.Api.Import;

namespace Practice.Api.Controllers;

[ApiController]
[Route("api/imports")]
public class ImportsController(CompanyImporter importer) : ControllerBase
{
    // Runs the whole import inside the request for now; step 8 moves it to the background
    [HttpPost]
    public async Task<ImportSummary> Start(CancellationToken ct) => 
        await importer.RunAsync(ct);
}