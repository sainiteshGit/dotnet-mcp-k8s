using System.Threading;
using System.Threading.Tasks;
using Azure.Core;
using FluentAssertions;
using WebApp.Persistence;

namespace WebApp.Tests.Persistence;

public class PostgresConnectionStringBuilderTests
{
    [Fact]
    public async Task Build_includes_host_database_username_and_token_as_password()
    {
        var credential = new StaticTokenCredential("opaque-jwt-token-value");

        var connStr = await PostgresConnectionStringBuilder.BuildAsync(
            host: "pg.example.postgres.database.azure.com",
            database: "taskmgr",
            username: "uami-taskmgr",
            credential: credential,
            cancellationToken: CancellationToken.None);

        connStr.Should().Contain("Host=pg.example.postgres.database.azure.com");
        connStr.Should().Contain("Database=taskmgr");
        connStr.Should().Contain("Username=uami-taskmgr");
        connStr.Should().Contain("Password=opaque-jwt-token-value");
        connStr.Should().Contain("SSL Mode=Require", "Entra-auth to Azure Postgres requires TLS");
    }

    [Fact]
    public async Task Build_requests_ossrdbms_aad_scope()
    {
        var credential = new StaticTokenCredential("t");
        await PostgresConnectionStringBuilder.BuildAsync(
            "h", "d", "u", credential, CancellationToken.None);

        credential.LastContext.Should().NotBeNull();
        credential.LastContext!.Value.Scopes.Should()
            .ContainSingle().Which.Should().Be("https://ossrdbms-aad.database.windows.net/.default");
    }

    private sealed class StaticTokenCredential : TokenCredential
    {
        private readonly string _token;
        public TokenRequestContext? LastContext { get; private set; }

        public StaticTokenCredential(string token) => _token = token;

        public override AccessToken GetToken(TokenRequestContext requestContext, CancellationToken cancellationToken)
        {
            LastContext = requestContext;
            return new AccessToken(_token, DateTimeOffset.UtcNow.AddHours(1));
        }

        public override ValueTask<AccessToken> GetTokenAsync(TokenRequestContext requestContext, CancellationToken cancellationToken)
        {
            LastContext = requestContext;
            return new ValueTask<AccessToken>(new AccessToken(_token, DateTimeOffset.UtcNow.AddHours(1)));
        }
    }
}
