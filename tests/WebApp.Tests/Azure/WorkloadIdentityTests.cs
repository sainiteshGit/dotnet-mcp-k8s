using FluentAssertions;
using WebApp.Azure;

namespace WebApp.Tests.Azure;

public class WorkloadIdentityTests
{
    [Fact]
    public void TryGetClientId_returns_value_from_AZURE_CLIENT_ID()
    {
        var env = new Dictionary<string, string?>
        {
            ["AZURE_CLIENT_ID"] = "11111111-2222-3333-4444-555555555555",
        };

        WorkloadIdentity.TryGetClientId(env.GetValueOrDefault, out var id).Should().BeTrue();
        id.Should().Be("11111111-2222-3333-4444-555555555555");
    }

    [Fact]
    public void TryGetClientId_returns_false_when_env_missing()
    {
        var env = new Dictionary<string, string?>();

        WorkloadIdentity.TryGetClientId(env.GetValueOrDefault, out var id).Should().BeFalse();
        id.Should().BeNull();
    }

    [Fact]
    public void TryGetClientId_returns_false_when_env_whitespace()
    {
        var env = new Dictionary<string, string?> { ["AZURE_CLIENT_ID"] = "   " };

        WorkloadIdentity.TryGetClientId(env.GetValueOrDefault, out var id).Should().BeFalse();
        id.Should().BeNull();
    }
}
