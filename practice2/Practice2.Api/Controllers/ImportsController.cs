using Microsoft.AspNetCore.Mvc;
using Practice2.Api.Import;

namespace Practice2.Api.Controllers;

[ApiController]
[Route("api/imports")]
public class ImportsController(CompanyImporter importer) : ControllerBase
{
    // Temporary: runs the import inside the request. Step 8 moves it to a background worker.
    [HttpPost]
    public async Task<ImportSummary> Run(CancellationToken ct)
        => await importer.RunAsync(ct);
}