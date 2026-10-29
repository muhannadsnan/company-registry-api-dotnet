using Practice.Api.Domain;

namespace Practice.Tests;

public class OrgnrTests
{
    [Theory]
    [InlineData("923609016")] // Equinor ASA
    [InlineData("974760673")] // Brønnøysundregistrene
    [InlineData("912855414")]
    public void Accepts_valid_numbers(string value)
    {
        Assert.True(Orgnr.IsValid(value));
    }

    [Theory]
    [InlineData("923609017")] // wrong check digit (Equinor is 923609016)
    [InlineData("12345678")] // too short
    [InlineData("1234567890")] // too long
    [InlineData("92360901A")] // not a digit
    [InlineData("")]
    [InlineData(null)]
    public void Rejects_invalid_values(string? value)
    {
        Assert.False(Orgnr.IsValid(value));
    }

    [Fact]
    public void Rejects_prefix_where_the_check_digit_10()
    {
        // 4x3=12, 12%11=1, 11-1=10: no digit can be valid here.
        for(var d = 0; d <= 9; d++)
        {
            Assert.False(Orgnr.IsValid($"00000040{d}"));
        }
    }
}