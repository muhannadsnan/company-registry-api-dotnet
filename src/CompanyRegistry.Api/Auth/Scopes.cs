using Microsoft.AspNetCore.Authorization;

namespace CompanyRegistry.Api.Auth;

/// <summary>
/// The scopes this API understands. Each one is also the name of an authorization policy,
/// the same way an API protected by Maskinporten checks the scope claim in the access token.
/// </summary>
public static class Scopes
{
    public const string CompaniesRead = "companies.read";
    public const string CompaniesImport = "companies.import";

    public const string ClaimType = "scope";

    /// <summary>Requires the token's space-separated "scope" claim to contain <paramref name="scope"/>.</summary>
    public static AuthorizationPolicyBuilder RequireScope(this AuthorizationPolicyBuilder policy, string scope) =>
        policy.RequireAssertion(context => context.User
            .FindAll(ClaimType)
            .SelectMany(claim => claim.Value.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            .Contains(scope, StringComparer.Ordinal));
}
