namespace CompanyRegistry.Api.Domain;

/// <summary>
/// Norwegian organisation number (organisasjonsnummer): nine digits, the last one a MOD11 check digit.
/// </summary>
public static class OrganisationNumber
{
    private static readonly int[] Weights = [3, 2, 7, 6, 5, 4, 3, 2];

    public static bool IsValid(string? value)
    {
        if (value is null || value.Length != 9 || !value.All(char.IsAsciiDigit))
        {
            return false;
        }

        var sum = 0;
        for (var i = 0; i < Weights.Length; i++)
        {
            sum += (value[i] - '0') * Weights[i];
        }

        var control = 11 - (sum % 11);
        if (control == 11)
        {
            control = 0;
        }

        // A remainder that gives 10 is never issued, so such a number is always invalid.
        return control != 10 && control == value[8] - '0';
    }
}
