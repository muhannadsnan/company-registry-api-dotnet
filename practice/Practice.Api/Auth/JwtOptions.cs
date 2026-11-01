using System.ComponentModel.DataAnnotations;

namespace Practice.Api.Auth;

public class JwtOptions
{
    public const string Section = "Jwt";

    [Required]
    public string Issuer { get; set; } = "";

    [Required]
    public string Audience { get; set; } = "";

    [Required, MinLength(32)]
    public string SigningKey { get; set; } = "";

    [Range(1, 1440)]
    public int LifetimeMinutes { get; set; } = 120;
}