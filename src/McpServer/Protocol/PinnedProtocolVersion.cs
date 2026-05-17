namespace McpServer.Protocol;

/// <summary>
/// The MCP protocol version pinned by this server (Constitution Principle I —
/// MCP Protocol Compliance NON-NEGOTIABLE).
///
/// Read in Program.cs when configuring the server and asserted by
/// <c>PinnedVersionTests</c>. Any change here is a constitutional event and
/// requires a constitution amendment + plan.md update.
/// </summary>
public static class PinnedProtocolVersion
{
    /// <summary>Pinned MCP spec date (YYYY-MM-DD).</summary>
    public const string Value = "2025-06-18";
}
