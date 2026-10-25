using System.ComponentModel.DataAnnotations;

namespace Practice.Api.Contracts;

public record CompanySearchQuery
{
    public string? Name { get; set; }
    public string? OrgForm { get; set; }
    
    [Range(1,1000)]
    public int Page { get; set; } = 1;

    [Range(1, 100)]
    public int PageSize { get; set; } = 20;
}