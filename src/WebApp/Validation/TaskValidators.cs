using FluentValidation;

namespace WebApp.Validation;

/// <summary>Allowed status / priority enum strings (lower_snake) — must match data-model.md.</summary>
internal static class TaskEnums
{
    public static readonly string[] Status = ["todo", "in_progress", "done"];
    public static readonly string[] Priority = ["low", "medium", "high"];
}

/// <summary>FluentValidation rules for <see cref="CreateTaskRequest"/> (T054).</summary>
public sealed class CreateTaskValidator : AbstractValidator<CreateTaskRequest>
{
    public CreateTaskValidator()
    {
        RuleFor(x => x.Title).NotEmpty().Length(1, 200);
        RuleFor(x => x.Description!).MaximumLength(2000).When(x => x.Description is not null);
        RuleFor(x => x.Status!).Must(s => TaskEnums.Status.Contains(s))
            .When(x => x.Status is not null)
            .WithMessage("status must be one of: todo, in_progress, done");
        RuleFor(x => x.Priority!).Must(p => TaskEnums.Priority.Contains(p))
            .When(x => x.Priority is not null)
            .WithMessage("priority must be one of: low, medium, high");
    }
}

/// <summary>FluentValidation rules for <see cref="PutTaskRequest"/> (T054).</summary>
public sealed class PutTaskValidator : AbstractValidator<PutTaskRequest>
{
    public PutTaskValidator()
    {
        RuleFor(x => x.Title).NotEmpty().Length(1, 200);
        RuleFor(x => x.Description!).MaximumLength(2000).When(x => x.Description is not null);
        RuleFor(x => x.Status!).Must(s => TaskEnums.Status.Contains(s))
            .When(x => x.Status is not null)
            .WithMessage("status must be one of: todo, in_progress, done");
        RuleFor(x => x.Priority!).Must(p => TaskEnums.Priority.Contains(p))
            .When(x => x.Priority is not null)
            .WithMessage("priority must be one of: low, medium, high");
    }
}

/// <summary>FluentValidation rules for <see cref="PatchTaskRequest"/> — empty body is invalid (T054).</summary>
public sealed class PatchTaskValidator : AbstractValidator<PatchTaskRequest>
{
    public PatchTaskValidator()
    {
        RuleFor(x => x).Must(x => x.HasAny).WithMessage("At least one field must be supplied.");
        RuleFor(x => x.Title!).Length(1, 200).When(x => x.Title is not null);
        RuleFor(x => x.Description!).MaximumLength(2000).When(x => x.Description is not null);
        RuleFor(x => x.Status!).Must(s => TaskEnums.Status.Contains(s))
            .When(x => x.Status is not null)
            .WithMessage("status must be one of: todo, in_progress, done");
        RuleFor(x => x.Priority!).Must(p => TaskEnums.Priority.Contains(p))
            .When(x => x.Priority is not null)
            .WithMessage("priority must be one of: low, medium, high");
    }
}

/// <summary>FluentValidation rules for <see cref="ListTasksQuery"/> (T054).</summary>
public sealed class ListTasksQueryValidator : AbstractValidator<ListTasksQuery>
{
    public ListTasksQueryValidator()
    {
        RuleFor(x => x.Page).GreaterThanOrEqualTo(1);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100);
        RuleFor(x => x.Status!).Must(s => TaskEnums.Status.Contains(s)).When(x => x.Status is not null);
        RuleFor(x => x.Priority!).Must(p => TaskEnums.Priority.Contains(p)).When(x => x.Priority is not null);
        // Filter window where due_before < due_after still returns 200 with empty result (per data-model.md).
    }
}
