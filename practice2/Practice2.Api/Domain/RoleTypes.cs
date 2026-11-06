namespace Practice2.Api.Domain;

// Brreg's role codes and their names (from /enhetsregisteret/api/roller/rolletyper).
// Only the codes are stored; a code missing here is shown as itself.
public static class RoleTypes
{
    private static readonly Dictionary<string, (string Name, int Rank)> Types = new()
    {
        ["DAGL"] = ("Daglig leder", 10),
        ["LEDE"] = ("Styrets leder", 20),
        ["NEST"] = ("Nestleder", 30),
        ["MEDL"] = ("Styremedlem", 40),
        ["VARA"] = ("Varamedlem", 50),
        ["OBS"] = ("Observatør", 60),
        ["REGN"] = ("Regnskapsfører", 70),
        ["REVI"] = ("Revisor", 80),
        ["FFØR"] = ("Forretningsfører", 900),
        ["BOBE"] = ("Bostyrer", 900),
        ["KONT"] = ("Kontaktperson", 900),
        ["KDEB"] = ("Konkursdebitor", 900),
    };

    public static string NameOf(string code)
        // => Types.GetValueOrDefault(code, code);
        // => Types.TryGetValue(code, out var type) ? type.Name : code;
         => Types.ContainsKey(code) ? Types[code].Name : code;
    
    public static int RankOf(string code) 
        // => Types.GetValueOrDefault(code, code);
        // => Types.ContainsKey(code) ? Types[code].Rank : 999;
        => Types.TryGetValue(code, out var type) ? type.Rank : 999;
}