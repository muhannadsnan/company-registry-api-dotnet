using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using Practice.Api.Contracts;
using Practice.Api.Data;

namespace Practice.Api.Auth;

public class TokenService(IOptions<JwtOptions> options)
{
    public static SymmetricSecurityKey CreateSigningKey(JwtOptions jwt) =>
        new(Encoding.UTF8.GetBytes(jwt.SigningKey));

    public LoginResponse CreateToken(User user)
    {
        var jwt = options.Value;
        var scopes = user.Scopes.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var expiresAt = DateTimeOffset.UtcNow.AddMinutes(jwt.LifetimeMinutes);

        var claims = new List<Claim>
        {
            new("sub", user.Id.ToString()),
            new("name", user.Username),
        };
        claims.AddRange(scopes.Select(scope => new Claim("scope", scope)));

        var token = new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
        {
            Issuer = jwt.Issuer,
            Audience = jwt.Audience,
            Subject = new ClaimsIdentity(claims),
            Expires = expiresAt.UtcDateTime,
            SigningCredentials = new SigningCredentials(CreateSigningKey(jwt), SecurityAlgorithms.HmacSha256),
        });

        return new LoginResponse(token, expiresAt, user.Username, scopes);
    }
}
