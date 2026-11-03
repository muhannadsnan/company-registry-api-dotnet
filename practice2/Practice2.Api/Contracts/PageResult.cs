namespace Practice2.Api.Contracts;

public record PageResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount);