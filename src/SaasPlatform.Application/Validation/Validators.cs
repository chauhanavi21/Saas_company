using FluentValidation;
using SaasPlatform.Application.Account;
using SaasPlatform.Application.Auth;
using SaasPlatform.Application.Projects;
using SaasPlatform.Application.Team;
using SaasPlatform.Domain.Common;
using SaasPlatform.Domain.Entities;

namespace SaasPlatform.Application.Validation;

internal static class Rules
{
    /// <summary>The built-in check accepts "user@host" with no dot, which can never receive mail, so we require a dotted domain.</summary>
    public static IRuleBuilderOptions<T, string> Email<T>(this IRuleBuilder<T, string> rule) =>
        rule.NotEmpty().WithMessage("Email is required.")
            .MaximumLength(320)
            .EmailAddress()
            .Must(e => e.Split('@')[^1].Contains('.') && !e.Split('@')[^1].EndsWith('.') && !e.Contains(' '))
            .WithMessage("Enter a valid email address.");

    /// <summary>At least 8 characters, no longer than 128 (long inputs make PBKDF2 an easy DoS target).</summary>
    public static IRuleBuilderOptions<T, string> NewPassword<T>(this IRuleBuilder<T, string> rule) =>
        rule.NotEmpty().WithMessage("Password is required.")
            .MinimumLength(8).WithMessage("Password must be at least 8 characters.")
            .MaximumLength(128).WithMessage("Password must be 128 characters or fewer.");

    public static IRuleBuilderOptions<T, string> Role<T>(this IRuleBuilder<T, string> rule) =>
        rule.Must(Roles.IsValid).WithMessage($"Role must be one of: {string.Join(", ", Roles.All)}.");
}

public class RegisterRequestValidator : AbstractValidator<RegisterRequest>
{
    public RegisterRequestValidator()
    {
        RuleFor(x => x.CompanyName).NotEmpty().WithMessage("Company name is required.").MaximumLength(200);
        RuleFor(x => x.FullName).NotEmpty().WithMessage("Your name is required.").MaximumLength(200);
        RuleFor(x => x.Email).Email();
        RuleFor(x => x.Password).NewPassword();
    }
}

public class LoginRequestValidator : AbstractValidator<LoginRequest>
{
    public LoginRequestValidator()
    {
        RuleFor(x => x.Email).NotEmpty().MaximumLength(320);
        RuleFor(x => x.Password).NotEmpty().MaximumLength(128);
    }
}

public class RefreshRequestValidator : AbstractValidator<RefreshRequest>
{
    public RefreshRequestValidator() => RuleFor(x => x.RefreshToken).NotEmpty().MaximumLength(200);
}

public class ChangePasswordRequestValidator : AbstractValidator<ChangePasswordRequest>
{
    public ChangePasswordRequestValidator()
    {
        RuleFor(x => x.CurrentPassword).NotEmpty().MaximumLength(128);
        RuleFor(x => x.NewPassword).NewPassword();
        RuleFor(x => x.NewPassword).NotEqual(x => x.CurrentPassword).WithMessage("Choose a different password.");
    }
}

public class UpdateProfileRequestValidator : AbstractValidator<UpdateProfileRequest>
{
    public UpdateProfileRequestValidator() =>
        RuleFor(x => x.FullName).NotEmpty().WithMessage("Your name is required.").MaximumLength(200);
}

public class ChangeRoleRequestValidator : AbstractValidator<ChangeRoleRequest>
{
    public ChangeRoleRequestValidator() => RuleFor(x => x.Role).Role();
}

public class CreateInvitationRequestValidator : AbstractValidator<CreateInvitationRequest>
{
    public CreateInvitationRequestValidator()
    {
        RuleFor(x => x.Email).Email();
        RuleFor(x => x.Role).Role();
    }
}

public class AcceptInvitationRequestValidator : AbstractValidator<AcceptInvitationRequest>
{
    public AcceptInvitationRequestValidator()
    {
        RuleFor(x => x.Token).NotEmpty().MaximumLength(200);
        RuleFor(x => x.FullName).NotEmpty().WithMessage("Your name is required.").MaximumLength(200);
        RuleFor(x => x.Password).NewPassword();
    }
}

public class SaveProjectRequestValidator : AbstractValidator<SaveProjectRequest>
{
    public SaveProjectRequestValidator()
    {
        RuleFor(x => x.Name).NotEmpty().WithMessage("Project name is required.")
            .MaximumLength(200).WithMessage("Project name must be 200 characters or fewer.");
        RuleFor(x => x.Description).MaximumLength(2000);
    }
}

public class CreateTaskRequestValidator : AbstractValidator<CreateTaskRequest>
{
    public CreateTaskRequestValidator()
    {
        RuleFor(x => x.Title).NotEmpty().WithMessage("Task title is required.")
            .MaximumLength(300).WithMessage("Task title must be 300 characters or fewer.");
        RuleFor(x => x.Description).MaximumLength(4000);
        RuleFor(x => x.Priority).Must(IsPriority).When(x => x.Priority is not null).WithMessage(PriorityMessage);
    }

    internal static bool IsPriority(string? v) => Enum.TryParse<TaskPriority>(v, true, out var p) && Enum.IsDefined(p) && !int.TryParse(v, out _);
    internal static string PriorityMessage => $"Priority must be one of: {string.Join(", ", Enum.GetNames<TaskPriority>())}.";
}

public class UpdateTaskRequestValidator : AbstractValidator<UpdateTaskRequest>
{
    public UpdateTaskRequestValidator()
    {
        RuleFor(x => x.Title).NotEmpty().WithMessage("Task title is required.")
            .MaximumLength(300).WithMessage("Task title must be 300 characters or fewer.");
        RuleFor(x => x.Description).MaximumLength(4000);
        RuleFor(x => x.Status)
            .Must(v => Enum.TryParse<ProjectTaskStatus>(v, true, out var s) && Enum.IsDefined(s) && !int.TryParse(v, out _))
            .WithMessage($"Status must be one of: {string.Join(", ", Enum.GetNames<ProjectTaskStatus>())}.");
        RuleFor(x => x.Priority).Must(CreateTaskRequestValidator.IsPriority).When(x => x.Priority is not null)
            .WithMessage(CreateTaskRequestValidator.PriorityMessage);
    }
}

public class AddCommentRequestValidator : AbstractValidator<AddCommentRequest>
{
    public AddCommentRequestValidator() =>
        RuleFor(x => x.Body).NotEmpty().WithMessage("Write something first.")
            .MaximumLength(2000).WithMessage("Comments must be 2000 characters or fewer.");
}
