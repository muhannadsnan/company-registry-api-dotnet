namespace Practice2.Api.Domain;

public static class Orgnr
{
    private static readonly int[] Weights = [3, 2, 7, 6, 5, 4, 3, 2];

    public static bool IsValid(string? value)
    {
        if (value is null || value.Length != 9 || !value.All(char.IsAsciiDigit))
            return false;

        var sum = 0;
        for (var i = 0; i < Weights.Length; i++)
        {
            sum += (value[i] - '0') * Weights[i];
        }

        var control = 11 - (sum % 11);
        if (control == 11)
            control = 0;
        else if (control == 10)
            return false;

        return control == value[8] - '0';
    }
}