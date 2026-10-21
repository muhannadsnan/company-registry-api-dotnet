using CompanyRegistry.Api.Auth;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace CompanyRegistry.Api.Controllers;

public sealed record TokenRequest
{
    [FromForm(Name = "grant_type")] public string? GrantType { get; init; }
    [FromForm(Name = "client_id")] public string? ClientId { get; init; }
    [FromForm(Name = "client_secret")] public string? ClientSecret { get; init; }
    [FromForm(Name = "scope")] public string? Scope { get; init; }
}

/// <summary>
/// OAuth 2.0 client-credentials token endpoint, for local development only.
/// Off unless Auth:EnableDevTokenEndpoint is true.
/// </summary>
[ApiController]
[Route("connect/token")]
[AllowAnonymous]
public sealed class TokenController(DevTokenIssuer issuer, IOptions<AuthOptions> options) : ControllerBase
{
    [HttpPost]
    [Consumes("application/x-www-form-urlencoded")]
    public IActionResult Token([FromForm] TokenRequest request)
    {
        if (!options.Value.EnableDevTokenEndpoint)
        {
            return NotFound();
        }
        if (request.GrantType != "client_credentials")
        {
            return BadRequest(new { error = "unsupported_grant_type" });
        }

        var token = issuer.Issue(request.ClientId ?? "", request.ClientSecret ?? "", request.Scope);
        if (token is null)
        {
            return Unauthorized(new { error = "invalid_client" });
        }

        return Ok(new
        {
            access_token = token.AccessToken,
            token_type = "Bearer",
            expires_in = token.ExpiresIn,
            scope = token.Scope,
        });
    }
}
