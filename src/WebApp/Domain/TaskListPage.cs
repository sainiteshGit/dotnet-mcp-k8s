using System.Collections.Generic;

namespace WebApp.Domain;

/// <summary>
/// Generic paged result envelope returned by list endpoints. Field names
/// serialise as <c>items</c>, <c>page</c>, <c>page_size</c>, <c>total</c>
/// per <c>contracts/webapp-openapi.yaml</c>.
/// </summary>
public sealed record TaskListPage<T>(
    IReadOnlyList<T> Items,
    int Page,
    int PageSize,
    long Total);
