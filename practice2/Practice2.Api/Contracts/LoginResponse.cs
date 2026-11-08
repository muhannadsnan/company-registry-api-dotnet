namespace Practice2.Api.Contracts;

public record LoginResponse(string Token, DateTimeOffset ExpiresAt, string Username, string[] Scopes);