using Microsoft.AspNetCore.Mvc;
using Practice.Api.Import;

namespace Practice.Api.Controllers;

// TGemporary: shows what the client fetches. Removed in step 7.
[ApiController]
[Route("api/brreg-preview")]
public class BrregPreviewController(BrregClient brreg) : ControllerBase
{
    [HttpGet]
    public async Task<BrregPage> Get(CancellationToken ct) => 
        await brreg.GetPageAsync(page: 0, size: 5, ct);
}