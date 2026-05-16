using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using Testcontainers.PostgreSql;
using WebApp.Persistence;

namespace WebApp.Tests.Integration;

/// <summary>
/// xUnit class fixture (one shared Postgres container per test class) that
/// boots the real WebApp via <see cref="WebApplicationFactory{TEntryPoint}"/>
/// pointed at a Testcontainers Postgres instance and applies the EF Core
/// migrations on startup. Tests T041-T049 share this rig.
/// </summary>
public sealed class PostgresFixture : IAsyncLifetime
{
    private readonly PostgreSqlContainer _container =
        new PostgreSqlBuilder()
            .WithImage("postgres:16-alpine")
            .WithDatabase("taskmgr_test")
            .WithUsername("postgres")
            .WithPassword("postgres")
            .Build();

    public WebApplicationFactory<Program> Factory { get; private set; } = default!;

    public string ConnectionString => _container.GetConnectionString();

    public async Task InitializeAsync()
    {
        await _container.StartAsync();

        Factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.UseEnvironment("Development");
                builder.ConfigureAppConfiguration((_, cfg) =>
                {
                    cfg.AddInMemoryCollection(new Dictionary<string, string?>
                    {
                        ["ConnectionStrings:Postgres"] = ConnectionString,
                    });
                });
            });

        // Force the host to build, then apply migrations.
        using var scope = Factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TaskDbContext>();
        await db.Database.MigrateAsync();
    }

    public async Task DisposeAsync()
    {
        Factory.Dispose();
        await _container.DisposeAsync();
    }
}
