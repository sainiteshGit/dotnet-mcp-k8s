using Microsoft.EntityFrameworkCore;
using WebApp.Domain;

namespace WebApp.Persistence;

/// <summary>
/// Data-access abstraction over <see cref="TaskDbContext"/> for the
/// <c>/api/v1/tasks</c> endpoints (T055). Concurrency is last-writer-wins
/// for v1 (no RowVersion / xmin); CHECK constraints in
/// <c>TaskDbContext.OnModelCreating</c> defend the DB against bad enum values.
/// </summary>
public interface ITaskRepository
{
    Task<TaskItem?> GetAsync(Guid id, CancellationToken ct);

    Task<TaskListPage<TaskItem>> ListAsync(
        Domain.TaskStatus? status,
        Domain.TaskPriority? priority,
        DateOnly? dueBefore,
        DateOnly? dueAfter,
        int page,
        int pageSize,
        CancellationToken ct);

    Task<TaskItem> CreateAsync(TaskItem item, CancellationToken ct);

    Task<TaskItem?> ReplaceAsync(Guid id, Action<TaskItem> apply, CancellationToken ct);

    Task<TaskItem?> PatchAsync(Guid id, Action<TaskItem> apply, CancellationToken ct);

    Task<bool> DeleteAsync(Guid id, CancellationToken ct);
}

/// <summary>EF Core implementation of <see cref="ITaskRepository"/>.</summary>
public sealed class TaskRepository : ITaskRepository
{
    private readonly TaskDbContext _db;
    private readonly TimeProvider _time;

    public TaskRepository(TaskDbContext db, TimeProvider time)
    {
        _db = db;
        _time = time;
    }

    public Task<TaskItem?> GetAsync(Guid id, CancellationToken ct) =>
        _db.Tasks.AsNoTracking().FirstOrDefaultAsync(t => t.Id == id, ct);

    public async Task<TaskListPage<TaskItem>> ListAsync(
        Domain.TaskStatus? status,
        Domain.TaskPriority? priority,
        DateOnly? dueBefore,
        DateOnly? dueAfter,
        int page,
        int pageSize,
        CancellationToken ct)
    {
        var query = _db.Tasks.AsNoTracking().AsQueryable();
        if (status is not null) query = query.Where(t => t.Status == status.Value);
        if (priority is not null) query = query.Where(t => t.Priority == priority.Value);
        if (dueBefore is not null) query = query.Where(t => t.DueDate != null && t.DueDate <= dueBefore.Value);
        if (dueAfter is not null) query = query.Where(t => t.DueDate != null && t.DueDate >= dueAfter.Value);

        var total = await query.LongCountAsync(ct).ConfigureAwait(false);
        var items = await query
            .OrderByDescending(t => t.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct)
            .ConfigureAwait(false);

        return new TaskListPage<TaskItem>(items, page, pageSize, total);
    }

    public async Task<TaskItem> CreateAsync(TaskItem item, CancellationToken ct)
    {
        if (item.Id == Guid.Empty) item.Id = Guid.NewGuid();
        var now = _time.GetUtcNow().UtcDateTime;
        item.CreatedAt = now;
        item.UpdatedAt = now;
        _db.Tasks.Add(item);
        await _db.SaveChangesAsync(ct).ConfigureAwait(false);
        return item;
    }

    public async Task<TaskItem?> ReplaceAsync(Guid id, Action<TaskItem> apply, CancellationToken ct)
    {
        var existing = await _db.Tasks.FirstOrDefaultAsync(t => t.Id == id, ct).ConfigureAwait(false);
        if (existing is null) return null;
        apply(existing);
        existing.UpdatedAt = _time.GetUtcNow().UtcDateTime;
        await _db.SaveChangesAsync(ct).ConfigureAwait(false);
        return existing;
    }

    public async Task<TaskItem?> PatchAsync(Guid id, Action<TaskItem> apply, CancellationToken ct)
    {
        var existing = await _db.Tasks.FirstOrDefaultAsync(t => t.Id == id, ct).ConfigureAwait(false);
        if (existing is null) return null;
        apply(existing);
        existing.UpdatedAt = _time.GetUtcNow().UtcDateTime;
        await _db.SaveChangesAsync(ct).ConfigureAwait(false);
        return existing;
    }

    public async Task<bool> DeleteAsync(Guid id, CancellationToken ct)
    {
        var existing = await _db.Tasks.FirstOrDefaultAsync(t => t.Id == id, ct).ConfigureAwait(false);
        if (existing is null) return false;
        _db.Tasks.Remove(existing);
        await _db.SaveChangesAsync(ct).ConfigureAwait(false);
        return true;
    }
}
