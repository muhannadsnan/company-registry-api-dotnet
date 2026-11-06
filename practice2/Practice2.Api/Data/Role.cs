namespace Practice2.Api.Data;

public class Role
{
    public long Id { get; set; }

    public required string Orgnr { get; set; }
    public required string RoleType { get; set; }

    // The holder is either a person or another unit, never both.
    public string? PersonName { get; set; }
    public int? BirthYear { get; set; }
    public string? UnitOrgnr { get; set; }
    public string? UnitName { get; set; }

    public bool Resigned { get; set; }

    public Company? Company { get; set; }
}