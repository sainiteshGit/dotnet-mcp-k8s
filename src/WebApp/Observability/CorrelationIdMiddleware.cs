using Serilog.Context;

namespace WebApp.Observability;

/// <summary>
/// Reads (or generates) <c>X-Correlation-Id</c> on every request, pushes it
/// to Serilog <c>LogContext</c> and the current Activity, and echoes it on
/// the response. Implements FR-041/FR-042.
/// </summary>
public sealed class CorrelationIdMiddleware
{
    public const string HeaderName = "X-Correlation-Id";
    public const string ActivityAttributeName = "correlation.id";
    public const string LogPropertyName = "CorrelationId";

    private readonly RequestDelegate _next;

    public CorrelationIdMiddleware(RequestDelegate next) => _next = next;

    public async Task InvokeAsync(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var id = ExtractOrGenerate(context);

        // Echo on response. Use OnStarting so we don't overwrite an already-flushed header.
        context.Response.OnStarting(() =>
        {
            context.Response.Headers[HeaderName] = id;
            return Task.CompletedTask;
        });

        // Attach to the current Activity (created by OTel AspNetCore instrumentation).
        System.Diagnostics.Activity.Current?.SetTag(ActivityAttributeName, id);

        using (LogContext.PushProperty(LogPropertyName, id))
        {
            await _next(context);
        }
    }

    private static string ExtractOrGenerate(HttpContext context)
    {
        if (context.Request.Headers.TryGetValue(HeaderName, out var values))
        {
            var inbound = values.ToString();
            if (!string.IsNullOrWhiteSpace(inbound))
            {
                return inbound;
            }
        }

        // ULID-compatible 26-char Crockford base32 of 128 random bits.
        return GenerateUlid();
    }

    private static readonly char[] CrockfordAlphabet =
        "0123456789ABCDEFGHJKMNPQRSTVWXYZ".ToCharArray();

    private static string GenerateUlid()
    {
        // 48 bits time + 80 bits randomness, encoded as 26 base32 chars.
        Span<byte> bytes = stackalloc byte[16];
        var ms = (ulong)DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        bytes[0] = (byte)(ms >> 40);
        bytes[1] = (byte)(ms >> 32);
        bytes[2] = (byte)(ms >> 24);
        bytes[3] = (byte)(ms >> 16);
        bytes[4] = (byte)(ms >> 8);
        bytes[5] = (byte)ms;
        System.Security.Cryptography.RandomNumberGenerator.Fill(bytes[6..]);

        // Encode 128 bits as 26 Crockford base32 chars (130 bits worth; top 2 bits are zero-padded).
        Span<char> chars = stackalloc char[26];
        // Pull a 130-bit window: prepend 2 zero bits.
        var b = new byte[17];
        Array.Copy(bytes.ToArray(), 0, b, 1, 16);
        // bit offset 6 = start of first 5-bit group (after the 2-bit pad in b[0]).
        for (var i = 0; i < 26; i++)
        {
            var bitPos = 6 + i * 5;
            var byteIdx = bitPos / 8;
            var bitOff = bitPos % 8;
            int v;
            if (bitOff <= 3)
            {
                v = (b[byteIdx] >> (3 - bitOff)) & 0x1F;
            }
            else
            {
                var hi = b[byteIdx] << (bitOff - 3);
                var lo = b[byteIdx + 1] >> (11 - bitOff);
                v = (hi | lo) & 0x1F;
            }
            chars[i] = CrockfordAlphabet[v];
        }
        return new string(chars);
    }
}

public static class CorrelationIdMiddlewareExtensions
{
    public static IApplicationBuilder UseCorrelationId(this IApplicationBuilder app) =>
        app.UseMiddleware<CorrelationIdMiddleware>();
}
