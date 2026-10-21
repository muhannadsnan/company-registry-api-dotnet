using System.ComponentModel.DataAnnotations;
using System.Linq.Expressions;
using CompanyRegistry.Api.Data;

namespace CompanyRegistry.Api.Contracts;

public sealed record CompanyDto(
    string OrganisationNumber,
    string Name,
    string OrganisationFormCode,
    string? OrganisationFormDescription,
    AddressDto Address,
    DateOnly? RegistrationDate,
    DateTimeOffset LastChangedAt)
{
    /// <summary>Used inside LINQ queries, so EF Core selects only these columns instead of loading the whole entity.</summary>
    public static readonly Expression<Func<Company, CompanyDto>> FromEntity = c => new CompanyDto(
        c.OrganisationNumber,
        c.Name,
        c.OrganisationFormCode,
        c.OrganisationFormDescription,
        new AddressDto(c.StreetAddress, c.PostalCode, c.PostalPlace, c.MunicipalityNumber, c.Municipality),
        c.RegistrationDate,
        c.LastChangedAt);
}

public sealed record AddressDto(
    string? Street,
    string? PostalCode,
    string? PostalPlace,
    string? MunicipalityNumber,
    string? Municipality);

public sealed record CompanySearchQuery
{
    /// <summary>Part of the company name, case-insensitive.</summary>
    [StringLength(100)]
    public string? Name { get; init; }

    /// <summary>Organisation form code, e.g. AS, ENK, NUF.</summary>
    [StringLength(10)]
    public string? OrganisationForm { get; init; }

    [StringLength(4)]
    public string? PostalCode { get; init; }

    public DateOnly? RegisteredFrom { get; init; }
    public DateOnly? RegisteredTo { get; init; }

    [Range(1, 10_000)]
    public int Page { get; init; } = 1;

    [Range(1, 100)]
    public int PageSize { get; init; } = 20;
}

public sealed record PagedResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount);
