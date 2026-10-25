namespace Practice.Api.Domain;

public static class Orgnr
{
    private static readonly int[] Weights = [3, 2, 7, 6, 5, 4, 3, 2];

    public static bool IsValid(string? value)
    {
        if (value is null || value.Length != 9)
            return false;

        var allIsDigit = value.All(char.IsAsciiDigit);
        if (!allIsDigit)
            return false;

        var sum = 0;
        
        for(var i = 0; i < Weights.Length; i++)
        {
            var v = value[i] - '0';
            sum += v * Weights[i];                
        }
        var control = 11 - (sum % 11);

        if (control == 11)
            control = 0;
        else if (control == 10)
            return false;

        return control == value[8] - '0';
    }
}