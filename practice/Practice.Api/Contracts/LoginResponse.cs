namespace Practice.Api.Contracts;

public record LoginResponse(string AccessToken, DateTimeOffset ExpiresAt, string Username, string[] Scopes);