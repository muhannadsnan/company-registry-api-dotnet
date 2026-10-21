using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace CompanyRegistry.Api.Auth;

public sealed record IssuedToken(string AccessToken, int ExpiresIn, string Scope);

/// <summary>
/// Stand-in for Maskinporten in local development: it checks a client id and secret against
/// configuration and signs a short-lived JWT carrying the client's scopes. In production this
/// class goes away and the API trusts tokens signed by Maskinporten instead.
/// </summary>
public sealed class DevTokenIssuer(IOptions<AuthOptions> options)
{
    public IssuedToken? Issue(string clientId, string clientSecret, string? requestedScope)
    {
        var o = options.Value;
        var client = o.Clients.FirstOrDefault(c => c.ClientId == clientId);
        if (client is null || !SecretsMatch(client.ClientSecret, clientSecret))
        {
            return null;
        }

        // A client may ask for fewer scopes than it is allowed, never more.
        var scopes = requestedScope is null
            ? client.Scopes
            : requestedScope.Split(' ', StringSplitOptions.RemoveEmptyEntries).Intersect(client.Scopes).ToList();
        var scope = string.Join(' ', scopes);

        var token = new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
        {
            Issuer = o.Issuer,
            Audience = o.Audience,
            Expires = DateTime.UtcNow.Add(o.TokenLifetime),
            Subject = new ClaimsIdentity(
            [
                new Claim("sub", client.ClientId),
                new Claim("client_id", client.ClientId),
                new Claim(Scopes.ClaimType, scope),
                new Claim("jti", Guid.NewGuid().ToString("N")),
            ]),
            SigningCredentials = new SigningCredentials(SigningKey(o), SecurityAlgorithms.HmacSha256),
        });

        return new IssuedToken(token, (int)o.TokenLifetime.TotalSeconds, scope);
    }

    public static SymmetricSecurityKey SigningKey(AuthOptions o) => new(Encoding.UTF8.GetBytes(o.SigningKey));

    private static bool SecretsMatch(string expected, string actual) =>
        CryptographicOperations.FixedTimeEquals(Encoding.UTF8.GetBytes(expected), Encoding.UTF8.GetBytes(actual));
}
