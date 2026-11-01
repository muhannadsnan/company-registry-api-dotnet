using System.ComponentModel.DataAnnotations;

namespace Practice.Api.Contracts;

public record LoginRequest([Required] string Username, [Required] string Password);