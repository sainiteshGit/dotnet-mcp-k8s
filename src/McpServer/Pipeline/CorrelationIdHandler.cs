namespace McpServer.Pipeline;

/// <summary>
/// <see cref="DelegatingHandler"/> that stamps every outbound HTTP request with
/// <c>X-Correlation-Id</c> (T034/T035/T036). Resolution order:
/// <list type="number">
///   <item>If the request already carries the header, leave it alone.</item>
///   <item>Else if <see cref="CorrelationContext.Current"/> is set, forward that value.</item>
///   <item>Else generate a fresh ULID so backends always see a usable id.</item>
/// </list>
/// Matches the contract in <c>research.md §8</c>.
/// </summary>
public sealed class CorrelationIdHandler : DelegatingHandler
{
    public const string HeaderName = "X-Correlation-Id";

    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        if (!request.Headers.Contains(HeaderName))
        {
            var id = CorrelationContext.Current ?? UlidGenerator.New();
            request.Headers.TryAddWithoutValidation(HeaderName, id);
        }

        return base.SendAsync(request, cancellationToken);
    }
}
