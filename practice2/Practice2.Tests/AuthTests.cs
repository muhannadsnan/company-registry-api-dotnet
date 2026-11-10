using System.Net;
using System.Net.Http.Json;
using Practice2.Api.Contracts;
using Practice2.Tests.Infrastructure;

namespace Practice2.Tests;

[Collection("api")]
public class AuthTests(ApiFactory api)
{
    [Fact]
    public async Task Login_with_right_password_returns_a_token()
    {
        var client = api.CreateClient();

        var response = await client.PostAsJsonAsync("/api/auth/login",
            new { username = "reader", password = ApiFactory.Password });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<LoginResponse>();
        Assert.False(string.IsNullOrEmpty(body!.Token));
        Assert.Equal(["companies.read"], body.Scopes);
    }

    [Theory]
    [InlineData("reader", "wrong-password")]
    [InlineData("nobody", ApiFactory.Password)]
    public async Task Login_with_wrong_password_or_unknown_user_returns_401(string username, string password)
    {
        var client = api.CreateClient();

        var response = await client.PostAsJsonAsync("/api/auth/login", new { username, password });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }
}
