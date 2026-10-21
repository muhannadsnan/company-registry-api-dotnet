using System.ComponentModel.DataAnnotations;

namespace CompanyRegistry.Api.Auth;

public sealed class AuthOptions
{
    public const string Section = "Auth";

    [Required]
    public string Issuer { get; set; } = "";

    [Required]
    public string Audience { get; set; } = "";

    /// <summary>HMAC key for the development token issuer. At least 32 characters.</summary>
    [Required, MinLength(32)]
    public string SigningKey { get; set; } = "";

    public bool EnableDevTokenEndpoint { get; set; }

    public List<ClientOptions> Clients { get; set; } = [];

    public TimeSpan TokenLifetime { get; set; } = TimeSpan.FromHours(1);
}

public sealed class ClientOptions
{
    public string ClientId { get; set; } = "";
    public string ClientSecret { get; set; } = "";
    public List<string> Scopes { get; set; } = [];
}
