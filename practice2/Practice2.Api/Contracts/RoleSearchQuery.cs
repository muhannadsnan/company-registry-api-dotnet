using System.ComponentModel.DataAnnotations;

public record RolesSearchQuery
{
    public string? Name { get; set; }
    public string? RoleType { get; set; }
    public string? Orgnr { get; set; }

    [Range(1900,2100)]
    public int? BirthYearFrom { get; set; }

    [Range(1900,2100)]
    public int? BirthYearTo { get; set; }

    [Range(1, 1000)]
    public int Page { get; set; } = 1;

    [Range(1, 100)]
    public int PageSize { get; set; } = 20;
}