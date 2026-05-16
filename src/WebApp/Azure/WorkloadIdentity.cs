namespace WebApp.Azure;

/// <summary>
/// Resolves the User-Assigned Managed Identity (UAMI) client id that
/// Azure AKS Workload Identity projects into the pod via
/// <c>AZURE_CLIENT_ID</c> (T037). The reader is injectable so tests can
/// pass a fake environment without mutating process state.
/// </summary>
public static class WorkloadIdentity
{
    public const string ClientIdEnvVar = "AZURE_CLIENT_ID";

    /// <summary>
    /// Reads the UAMI client id from <paramref name="environment"/>.
    /// Returns <c>false</c> (and <paramref name="clientId"/> <c>null</c>)
    /// when the variable is missing or whitespace, so callers can fall
    /// back to plain <c>DefaultAzureCredential</c> in local dev.
    /// </summary>
    public static bool TryGetClientId(Func<string, string?> environment, out string? clientId)
    {
        ArgumentNullException.ThrowIfNull(environment);
        var raw = environment(ClientIdEnvVar);
        if (string.IsNullOrWhiteSpace(raw))
        {
            clientId = null;
            return false;
        }
        clientId = raw;
        return true;
    }

    /// <summary>Convenience overload that reads from process environment.</summary>
    public static bool TryGetClientId(out string? clientId) =>
        TryGetClientId(Environment.GetEnvironmentVariable, out clientId);
}
