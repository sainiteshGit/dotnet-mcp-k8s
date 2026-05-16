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
            var id = CorrelationContext.Current ?? GenerateUlid();
            request.Headers.TryAddWithoutValidation(HeaderName, id);
        }

        return base.SendAsync(request, cancellationToken);
    }

    private static readonly char[] CrockfordAlphabet =
        "0123456789ABCDEFGHJKMNPQRSTVWXYZ".ToCharArray();

    private static string GenerateUlid()
    {
        // 48-bit ms timestamp + 80 random bits, encoded as 26 Crockford base32 chars.
        Span<byte> bytes = stackalloc byte[16];
        var ms = (ulong)DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        bytes[0] = (byte)(ms >> 40);
        bytes[1] = (byte)(ms >> 32);
        bytes[2] = (byte)(ms >> 24);
        bytes[3] = (byte)(ms >> 16);
        bytes[4] = (byte)(ms >> 8);
        bytes[5] = (byte)ms;
        System.Security.Cryptography.RandomNumberGenerator.Fill(bytes[6..]);

        Span<char> chars = stackalloc char[26];
        var b = new byte[17];
        bytes.CopyTo(b.AsSpan(1));
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
