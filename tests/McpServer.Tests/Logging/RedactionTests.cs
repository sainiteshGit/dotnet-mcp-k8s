using FluentAssertions;
using McpServer.Observability;

namespace McpServer.Tests.Logging;

public class RedactionTests
{
    [Theory]
    [InlineData("API_TOKEN")]
    [InlineData("MY_API_KEY")]
    [InlineData("DB_PASSWORD")]
    [InlineData("CLIENT_SECRET")]
    [InlineData("AZURE_CLIENT_ID")]
    [InlineData("AZURE_TENANT_ID")]
    [InlineData("authorization")]
    [InlineData("Cookie")]
    [InlineData("X-Api-Key")]
    public void ShouldRedactKey_returns_true_for_sensitive_key_names(string key) =>
        Redactor.ShouldRedactKey(key).Should().BeTrue();

    [Theory]
    [InlineData("name")]
    [InlineData("status")]
    [InlineData("id")]
    [InlineData("title")]
    public void ShouldRedactKey_returns_false_for_benign_key_names(string key) =>
        Redactor.ShouldRedactKey(key).Should().BeFalse();

    [Theory]
    [InlineData("Bearer eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.payload.sig")]
    [InlineData("eyJhbGciOiJIUzI1NiJ9.eyJzdWIiOiIxMjM0NTY3ODkwIn0.SflKxwRJSMeKKF2QT4fwpMeJf36POk6yJV_adQssw5c")]
    [InlineData("Host=foo;Username=bar;Password=mySuperSecret123;Database=app")]
    [InlineData("postgresql://user:mySuperSecret123@db.example.com:5432/app")]
    [InlineData("https://example.com/cb?access_token=ya29.mySuperSecret123&other=ok")]
    public void RedactValue_replaces_sensitive_patterns_with_marker(string input)
    {
        var redacted = Redactor.RedactValue(input);

        redacted.Should().Contain("***REDACTED***");
        redacted.Should().NotContain("mySuperSecret123");
        redacted.Should().NotContain("eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.payload.sig");
        redacted.Should().NotContain("SflKxwRJSMeKKF2QT4fwpMeJf36POk6yJV_adQssw5c");
    }

    [Fact]
    public void RedactValue_returns_input_unchanged_when_no_sensitive_pattern_present()
    {
        const string clean = "task title: write the report";
        Redactor.RedactValue(clean).Should().Be(clean);
    }
}
