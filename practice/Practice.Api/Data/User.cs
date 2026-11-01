namespace Practice.Api.Data;

public class User
{
    public int Id { get; set; }
    public required string Username { get; set; }
    public required string PasswordHash { get; set; }
    public required string Scopes { get; set; }
}