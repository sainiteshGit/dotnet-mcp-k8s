using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using WebApp.Domain;
using WebApp.Persistence;

namespace WebApp.Tests.Integration;

/// <summary>
/// T048 — Initial migration applies cleanly, CHECK constraints reject
/// invalid rows at the DB layer, and the two custom indexes exist.
/// </summary>
[Trait("Category", "Integration")]
[Collection("Postgres")]
public sealed class PostgresPersistenceTests
{
    private readonly PostgresFixture _fx;
    public PostgresPersistenceTests(PostgresFixture fx) => _fx = fx;

    [Fact]
    public async Task Tasks_table_and_required_indexes_exist()
    {
        await using var conn = new NpgsqlConnection(_fx.ConnectionString);
        await conn.OpenAsync();
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = """
            SELECT indexname FROM pg_indexes
            WHERE tablename = 'tasks';
        """;
        var names = new System.Collections.Generic.List<string>();
        await using var r = await cmd.ExecuteReaderAsync();
        while (await r.ReadAsync())
        {
            names.Add(r.GetString(0));
        }
        names.Should().Contain("ix_tasks_status_priority_due_date");
        names.Should().Contain("ix_tasks_created_at_desc");
    }

    [Fact]
    public async Task Check_constraint_rejects_unknown_status()
    {
        using var scope = _fx.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TaskDbContext>();
        var conn = db.Database.GetDbConnection();
        if (conn.State != System.Data.ConnectionState.Open)
        {
            await conn.OpenAsync();
        }
        await using var cmd = conn.CreateCommand();
        cmd.CommandText = "INSERT INTO tasks (id, title, status, priority, created_at, updated_at) " +
                          "VALUES (gen_random_uuid(), 'x', 'nope', 'low', now(), now())";
        var act = async () => await cmd.ExecuteNonQueryAsync();
        await act.Should().ThrowAsync<PostgresException>();
    }

    [Fact]
    public async Task Migration_applied_and_round_trip_via_dbcontext_works()
    {
        using var scope = _fx.Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TaskDbContext>();
        var item = new TaskItem
        {
            Id = System.Guid.NewGuid(),
            Title = "round-trip",
            Status = WebApp.Domain.TaskStatus.Todo,
            Priority = TaskPriority.Medium,
            CreatedAt = System.DateTime.UtcNow,
            UpdatedAt = System.DateTime.UtcNow,
        };
        db.Tasks.Add(item);
        await db.SaveChangesAsync();
        (await db.Tasks.FindAsync(item.Id)).Should().NotBeNull();
    }
}
