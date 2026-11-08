using System.ComponentModel.DataAnnotations;

namespace Practice2.Api.Contracts;

public record LoginRequest([Required] string Username, [Required] string Password);