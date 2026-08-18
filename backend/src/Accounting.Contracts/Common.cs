namespace Accounting.Contracts;

/// <summary>Sayfalı yanıt — tüm liste uç noktalarının ortak biçimi.</summary>
public sealed record PagedResponse<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount);
