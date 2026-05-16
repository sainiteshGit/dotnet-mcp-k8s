using System.Text.RegularExpressions;

namespace McpServer.Observability;

/// <summary>
/// Shared, pure redaction policy. Mirrors <c>research.md §7</c> and the
/// implementation in <c>WebApp.Observability.Redactor</c>.
/// </summary>
public static partial class Redactor
{
    public const string Marker = "***REDACTED***";

    private static readonly string[] SensitiveSuffixes =
        ["_TOKEN", "_KEY", "_PASSWORD", "_SECRET"];

    private static readonly HashSet<string> SensitiveHeaderNames =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "authorization",
            "cookie",
            "set-cookie",
            "proxy-authorization",
            "x-api-key",
        };

    [GeneratedRegex(@"Bearer\s+[A-Za-z0-9\-_.=]+", RegexOptions.IgnoreCase)]
    private static partial Regex BearerRegex();

    [GeneratedRegex(@"eyJ[A-Za-z0-9_\-]+\.[A-Za-z0-9_\-]+\.[A-Za-z0-9_\-]+")]
    private static partial Regex JwtRegex();

    [GeneratedRegex(@"(?i)(password|pwd)\s*=\s*[^;""\s]+")]
    private static partial Regex ConnStrPasswordRegex();

    [GeneratedRegex(@"(?<scheme>[a-zA-Z][a-zA-Z0-9+\-.]*)://(?<user>[^:@/\s]+):(?<pwd>[^@/\s]+)@")]
    private static partial Regex UrlBasicAuthRegex();

    [GeneratedRegex(@"(?i)([?&])(access_token|token|api_key|apikey|key|secret|password)=([^&\s#]+)")]
    private static partial Regex UrlQuerySecretRegex();

    public static bool ShouldRedactKey(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return false;
        }

        if (SensitiveHeaderNames.Contains(name))
        {
            return true;
        }

        var upper = name.ToUpperInvariant();
        if (upper.StartsWith("AZURE_", StringComparison.Ordinal))
        {
            return true;
        }

        foreach (var suffix in SensitiveSuffixes)
        {
            if (upper.EndsWith(suffix, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    public static string RedactValue(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return value;
        }

        var redacted = value;
        redacted = JwtRegex().Replace(redacted, Marker);
        redacted = BearerRegex().Replace(redacted, "Bearer " + Marker);
        redacted = ConnStrPasswordRegex().Replace(redacted, m => $"{m.Groups[1].Value}={Marker}");
        redacted = UrlBasicAuthRegex().Replace(redacted, m => $"{m.Groups["scheme"].Value}://{Marker}@");
        redacted = UrlQuerySecretRegex().Replace(redacted, m => $"{m.Groups[1].Value}{m.Groups[2].Value}={Marker}");
        return redacted;
    }
}
