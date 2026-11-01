using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Practice.Api.Auth;
using Practice.Api.Contracts;
using Practice.Api.Data;

namespace Practice.Api.Controllers;

[ApiController]
[Route("api/auth")]
public class AuthController(AppDbContext db, IPasswordHasher<User> hasher, TokenService tokens) : ControllerBase
{
    [HttpPost("login")]
    public async Task<ActionResult<LoginResponse>> Login(LoginRequest request, CancellationToken ct)
    {
        var user = await db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Username == request.Username, ct);

        // The same answer for an unknown user and a wrong password, so callers can't find out which usernames exist.
        if (user is null || hasher.VerifyHashedPassword(user, user.PasswordHash, request.Password) == PasswordVerificationResult.Failed)
        {
            return Problem(statusCode: StatusCodes.Status401Unauthorized, title: "Wrong username or password");
        }

        return tokens.CreateToken(user);
    }
}
