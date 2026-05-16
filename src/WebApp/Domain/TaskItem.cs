using System;

namespace WebApp.Domain;

/// <summary>Lifecycle status of a <see cref="TaskItem"/>. Serialised as lower_snake.</summary>
public enum TaskStatus
{
    Todo,
    InProgress,
    Done,
}

/// <summary>Importance level of a <see cref="TaskItem"/>. Serialised as lower_snake.</summary>
public enum TaskPriority
{
    Low,
    Medium,
    High,
}

/// <summary>
/// Task aggregate root. Field shape mirrors <c>specs/001-task-manager-api/data-model.md</c>.
/// Concurrency is last-writer-wins for v1 (no RowVersion / xmin).
/// </summary>
public sealed class TaskItem
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Description { get; set; }
    public TaskStatus Status { get; set; } = TaskStatus.Todo;
    public TaskPriority Priority { get; set; } = TaskPriority.Medium;
    public DateOnly? DueDate { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
}
