using Azure.Core;
using Npgsql;

namespace WebApp.Persistence;

/// <summary>
/// Builds an Npgsql connection string that authenticates to Azure Database
/// for PostgreSQL Flexible Server via Microsoft Entra ID (T038).
/// The password slot is filled with a short-lived JWT obtained from the
/// supplied <see cref="TokenCredential"/> against the
/// <c>https://ossrdbms-aad.database.windows.net/.default</c> scope, as
/// documented in <c>research.md §4</c>.
/// </summary>
public static class PostgresConnectionStringBuilder
{
    public const string PostgresAadScope = "https://ossrdbms-aad.database.windows.net/.default";

    public static async Task<string> BuildAsync(
        string host,
        string database,
        string username,
        TokenCredential credential,
        CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(host);
        ArgumentException.ThrowIfNullOrWhiteSpace(database);
        ArgumentException.ThrowIfNullOrWhiteSpace(username);
        ArgumentNullException.ThrowIfNull(credential);

        var token = await credential.GetTokenAsync(
            new TokenRequestContext(new[] { PostgresAadScope }),
            cancellationToken).ConfigureAwait(false);

        var builder = new NpgsqlConnectionStringBuilder
        {
            Host = host,
            Database = database,
            Username = username,
            Password = token.Token,
            SslMode = SslMode.Require,
        };
        return builder.ConnectionString;
    }
}
