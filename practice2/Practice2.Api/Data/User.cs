namespace Practice2.Api.Data;

public class User
{
    public int Id { get; set; }
    public required string Username { get; set; }
    public required string PasswordHash { get; set; }

    // Space-separated, like OAuth: "companies.read companies.import"
    public required string Scopes { get; set; }
}