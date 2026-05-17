namespace McpServer.Pipeline;

/// <summary>
/// 26-char Crockford-base32 ULID generator. Extracted from
/// <see cref="CorrelationIdHandler"/> so tools and tests can share the same
/// implementation when an inbound correlation id is absent.
/// </summary>
public static class UlidGenerator
{
    private static readonly char[] Alphabet =
        "0123456789ABCDEFGHJKMNPQRSTVWXYZ".ToCharArray();

    public static string New()
    {
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
            chars[i] = Alphabet[v];
        }
        return new string(chars);
    }
}
