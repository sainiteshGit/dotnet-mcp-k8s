using System.Net;
using System.Net.Http;
using FluentAssertions;
using McpServer.Pipeline;

namespace McpServer.Tests.Pipeline;

public class CorrelationIdHandlerTests
{
    [Fact]
    public void CorrelationContext_Current_is_null_by_default()
    {
        CorrelationContext.Current.Should().BeNull();
    }

    [Fact]
    public void CorrelationContext_PushScope_sets_then_restores_Current()
    {
        CorrelationContext.Current.Should().BeNull();
        using (CorrelationContext.PushScope("ABCDEFGHJK1234567890ABCDEF"))
        {
            CorrelationContext.Current.Should().Be("ABCDEFGHJK1234567890ABCDEF");
        }
        CorrelationContext.Current.Should().BeNull();
    }

    [Fact]
    public async Task Handler_forwards_ambient_correlation_id_as_header()
    {
        var captured = new CapturingHandler();
        var handler = new CorrelationIdHandler { InnerHandler = captured };
        using var client = new HttpClient(handler);

        using (CorrelationContext.PushScope("FIXEDID01234567890ABCDEFGH"))
        {
            await client.GetAsync(new Uri("http://example.invalid/"));
        }

        captured.LastRequest.Should().NotBeNull();
        captured.LastRequest!.Headers.TryGetValues(CorrelationIdHandler.HeaderName, out var values).Should().BeTrue();
        values!.Should().ContainSingle().Which.Should().Be("FIXEDID01234567890ABCDEFGH");
    }

    [Fact]
    public async Task Handler_generates_id_when_none_present()
    {
        var captured = new CapturingHandler();
        var handler = new CorrelationIdHandler { InnerHandler = captured };
        using var client = new HttpClient(handler);

        await client.GetAsync(new Uri("http://example.invalid/"));

        captured.LastRequest!.Headers.TryGetValues(CorrelationIdHandler.HeaderName, out var values).Should().BeTrue();
        var id = values!.Single();
        id.Should().NotBeNullOrWhiteSpace();
        id.Length.Should().Be(26, "generated correlation ids are 26-char ULIDs");
    }

    [Fact]
    public async Task Handler_preserves_explicit_header_set_by_caller()
    {
        var captured = new CapturingHandler();
        var handler = new CorrelationIdHandler { InnerHandler = captured };
        using var client = new HttpClient(handler);

        using var req = new HttpRequestMessage(HttpMethod.Get, "http://example.invalid/");
        req.Headers.Add(CorrelationIdHandler.HeaderName, "CALLER-PROVIDED-ID");

        await client.SendAsync(req);

        captured.LastRequest!.Headers.GetValues(CorrelationIdHandler.HeaderName).Single().Should().Be("CALLER-PROVIDED-ID");
    }

    private sealed class CapturingHandler : HttpMessageHandler
    {
        public HttpRequestMessage? LastRequest { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequest = request;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK));
        }
    }
}
