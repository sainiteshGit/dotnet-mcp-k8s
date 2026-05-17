using FluentAssertions;
using McpServer.Protocol;

namespace McpServer.Tests.Protocol;

/// <summary>
/// T071 — Protocol-version pinning test (Principle I).
/// The MCP spec date this server speaks is locked to <c>2025-06-18</c>; any
/// drift is a constitutional event and must surface as a failing test before
/// shipping.
/// </summary>
public class PinnedVersionTests
{
    [Fact]
    public void Pinned_protocol_version_is_2025_06_18()
    {
        PinnedProtocolVersion.Value.Should().Be("2025-06-18");
    }
}
