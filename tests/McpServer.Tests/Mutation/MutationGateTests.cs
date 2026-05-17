using FluentAssertions;
using McpServer.Mutation;
using Microsoft.Extensions.Configuration;
using ModelContextProtocol.Protocol;

namespace McpServer.Tests.Mutation;

/// <summary>
/// T074 — MutationGate semantics (Principle II — Read-Only by Default).
///
/// Constitution requires: only the literal value "true" (case-insensitive)
/// enables mutations. Every other value, empty, or unset → disabled.
///
/// When disabled, mutation tools must return the structured
/// <c>mutations_disabled</c> envelope WITHOUT issuing any backing HTTP call.
/// The HTTP "no call made" half is verified per-tool with WireMock in Turn B;
/// this file pins (a) the env-var matrix and (b) the envelope shape.
/// </summary>
public class MutationGateTests
{
    [Theory]
    [InlineData("true")]
    [InlineData("True")]
    [InlineData("TRUE")]
    [InlineData("tRuE")]
    public void IsEnabled_is_true_for_literal_true_case_insensitive(string raw)
    {
        var gate = new MutationGate(BuildConfig(raw));
        gate.IsEnabled.Should().BeTrue();
    }

    [Theory]
    [InlineData("false")]
    [InlineData("0")]
    [InlineData("1")]
    [InlineData("yes")]
    [InlineData("True ")] // trailing space — strictly not "true"
    [InlineData(" true")] // leading space
    [InlineData("")]
    public void IsEnabled_is_false_for_anything_other_than_true(string raw)
    {
        var gate = new MutationGate(BuildConfig(raw));
        gate.IsEnabled.Should().BeFalse();
    }

    [Fact]
    public void IsEnabled_is_false_when_env_var_unset()
    {
        var gate = new MutationGate(new ConfigurationBuilder().Build());
        gate.IsEnabled.Should().BeFalse();
    }

    [Fact]
    public void MutationsDisabledResult_For_returns_isError_true_with_user_facing_text()
    {
        var result = MutationsDisabledResult.For("create_task", "TESTCORRELATIONID0123456789");

        result.IsError.Should().BeTrue();
        result.Content.Should().ContainSingle();
        var block = result.Content[0] as TextContentBlock;
        block.Should().NotBeNull();
        block!.Text.Should().Be(MutationsDisabledResult.UserFacingText);
    }

    [Fact]
    public void MutationsDisabledResult_For_meta_carries_correlation_id_and_error_envelope()
    {
        const string corrId = "TESTCORRELATIONID0123456789";
        var result = MutationsDisabledResult.For("delete_task", corrId);

        result.Meta.Should().NotBeNull("envelope must carry _meta per contracts/mcp-tools.md");
        result.Meta!["correlationId"]!.GetValue<string>().Should().Be(corrId);

        var error = result.Meta["error"] as System.Text.Json.Nodes.JsonObject;
        error.Should().NotBeNull();
        error!["code"]!.GetValue<string>().Should().Be("mutations_disabled");
        error["message"]!.GetValue<string>().Should().Be(MutationsDisabledResult.EnvelopeMessage);

        var details = error["details"] as System.Text.Json.Nodes.JsonObject;
        details.Should().NotBeNull();
        details!["tool"]!.GetValue<string>().Should().Be("delete_task");
        details["remediation"]!.GetValue<string>().Should().Be(MutationsDisabledResult.Remediation);
    }

    private static IConfiguration BuildConfig(string value) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                [MutationGate.ConfigKey] = value,
            })
            .Build();
}
