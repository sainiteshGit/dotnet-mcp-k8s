using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using WebApp.Domain;

namespace WebApp.Persistence;

/// <summary>
/// EF Core context for the Task Manager. Enum properties are stored as
/// lower_snake strings and the two reporting indexes from
/// <c>data-model.md</c> are declared here. CHECK constraints and the
/// <c>pgcrypto</c> extension are emitted by the initial migration.
/// </summary>
public sealed class TaskDbContext : DbContext
{
    public TaskDbContext(DbContextOptions<TaskDbContext> options) : base(options) { }

    public DbSet<TaskItem> Tasks => Set<TaskItem>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasPostgresExtension("pgcrypto");

        // Expression trees cannot contain switch expressions, so use ternary chains.
        var statusConverter = new ValueConverter<Domain.TaskStatus, string>(
            v => v == Domain.TaskStatus.Done ? "done"
                : v == Domain.TaskStatus.InProgress ? "in_progress"
                : "todo",
            v => v == "done" ? Domain.TaskStatus.Done
                : v == "in_progress" ? Domain.TaskStatus.InProgress
                : Domain.TaskStatus.Todo);

        var priorityConverter = new ValueConverter<Domain.TaskPriority, string>(
            v => v == Domain.TaskPriority.High ? "high"
                : v == Domain.TaskPriority.Low ? "low"
                : "medium",
            v => v == "high" ? Domain.TaskPriority.High
                : v == "low" ? Domain.TaskPriority.Low
                : Domain.TaskPriority.Medium);

        modelBuilder.Entity<TaskItem>(b =>
        {
            b.ToTable("tasks", t =>
            {
                t.HasCheckConstraint("chk_tasks_status", "status IN ('todo','in_progress','done')");
                t.HasCheckConstraint("chk_tasks_priority", "priority IN ('low','medium','high')");
                t.HasCheckConstraint("chk_tasks_title_len", "char_length(title) BETWEEN 1 AND 200");
                t.HasCheckConstraint("chk_tasks_desc_len", "description IS NULL OR char_length(description) <= 2000");
            });
            b.HasKey(x => x.Id).HasName("pk_tasks");
            b.Property(x => x.Id).HasColumnName("id").ValueGeneratedNever();
            b.Property(x => x.Title).HasColumnName("title").HasMaxLength(200).IsRequired();
            b.Property(x => x.Description).HasColumnName("description").HasMaxLength(2000);
            b.Property(x => x.Status).HasColumnName("status").HasConversion(statusConverter).HasDefaultValue(Domain.TaskStatus.Todo).IsRequired();
            b.Property(x => x.Priority).HasColumnName("priority").HasConversion(priorityConverter).HasDefaultValue(Domain.TaskPriority.Medium).IsRequired();
            b.Property(x => x.DueDate).HasColumnName("due_date").HasColumnType("date");
            b.Property(x => x.CreatedAt).HasColumnName("created_at").HasColumnType("timestamptz").IsRequired();
            b.Property(x => x.UpdatedAt).HasColumnName("updated_at").HasColumnType("timestamptz").IsRequired();

            b.HasIndex(x => new { x.Status, x.Priority, x.DueDate })
                .HasDatabaseName("ix_tasks_status_priority_due_date");
            b.HasIndex(x => x.CreatedAt)
                .HasDatabaseName("ix_tasks_created_at_desc")
                .IsDescending();
        });
    }
}
