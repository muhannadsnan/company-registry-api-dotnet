namespace Practice2.Api.Domain;

// Brreg's role codes and their names (from /enhetsregisteret/api/roller/rolletyper).
// Only the codes are stored; a code missing here is shown as itself.
public static class RoleTypes
{
    private static readonly Dictionary<string, string> Names = new()
    {
        ["DAGL"] = "Daglig leder",
        ["LEDE"] = "Styrets leder",
        ["NEST"] = "Nestleder",
        ["MEDL"] = "Styremedlem",
        ["VARA"] = "Varamedlem",
        ["OBS"] = "Observatør",
        ["REGN"] = "Regnskapsfører",
        ["REVI"] = "Revisor",
        ["FFØR"] = "Forretningsfører",
        ["BOBE"] = "Bostyrer",
        ["KONT"] = "Kontaktperson",
        ["KDEB"] = "Konkursdebitor",
    };

    public static string TextOf(string code) => Names.GetValueOrDefault(code, code);
}