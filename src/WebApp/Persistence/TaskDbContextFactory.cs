using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace WebApp.Persistence;

/// <summary>
/// Design-time factory used by <c>dotnet ef</c> tooling (T055). At runtime
/// the host wires <see cref="TaskDbContext"/> via DI with an Entra-issued
/// token (see <see cref="PostgresConnectionStringBuilder"/>); design-time
/// commands never connect, but EF Core still needs a syntactically valid
/// Npgsql connection string to scaffold the model.
/// </summary>
public sealed class TaskDbContextFactory : IDesignTimeDbContextFactory<TaskDbContext>
{
    public TaskDbContext CreateDbContext(string[] args)
    {
        var host = Environment.GetEnvironmentVariable("POSTGRES_HOST") ?? "localhost";
        var database = Environment.GetEnvironmentVariable("POSTGRES_DATABASE") ?? "taskmgr";
        var username = Environment.GetEnvironmentVariable("POSTGRES_USER") ?? "postgres";
        var password = Environment.GetEnvironmentVariable("POSTGRES_PASSWORD") ?? "postgres";

        var connStr = $"Host={host};Database={database};Username={username};Password={password}";

        var options = new DbContextOptionsBuilder<TaskDbContext>()
            .UseNpgsql(connStr, npg => npg.MigrationsHistoryTable("__ef_migrations_history"))
            .Options;

        return new TaskDbContext(options);
    }
}
