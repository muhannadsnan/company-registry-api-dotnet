using CompanyRegistry.Api.Domain;

namespace CompanyRegistry.Tests;

public class OrganisationNumberTests
{
    [Theory]
    [InlineData("923609016")] // Equinor ASA
    [InlineData("974760673")] // Brønnøysundregistrene
    [InlineData("916627939")]
    [InlineData("912855414")]
    public void Accepts_real_organisation_numbers(string value) =>
        Assert.True(OrganisationNumber.IsValid(value));

    [Theory]
    [InlineData("923609017")]   // wrong check digit
    [InlineData("974760670")]   // wrong check digit
    [InlineData("12345678")]    // too short
    [InlineData("1234567890")]  // too long
    [InlineData("92360901A")]   // not a digit
    [InlineData("923 609 016")] // spaces are not normalised here
    [InlineData("")]
    [InlineData(null)]
    public void Rejects_invalid_values(string? value) =>
        Assert.False(OrganisationNumber.IsValid(value));

    [Fact]
    public void Rejects_numbers_whose_check_digit_would_be_10()
    {
        // Weighted sum 4 * 3 = 12, remainder 1, so 11 - 1 = 10: no valid check digit exists for this prefix.
        Assert.All(Enumerable.Range(0, 10), d => Assert.False(OrganisationNumber.IsValid($"00000040{d}")));
    }
}
