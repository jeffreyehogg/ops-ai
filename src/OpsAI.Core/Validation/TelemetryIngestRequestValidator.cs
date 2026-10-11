using FluentValidation;
using OpsAI.Core.DTOs;

namespace OpsAI.Core.Validation;

public sealed class TelemetryIngestRequestValidator : AbstractValidator<TelemetryIngestRequest>
{
    public TelemetryIngestRequestValidator()
    {
        RuleFor(x => x.ServiceName)
            .NotEmpty().WithMessage("ServiceName is required.")
            .MaximumLength(100).WithMessage("ServiceName cannot exceed 100 characters.");

        RuleFor(x => x.Environment)
            .NotEmpty().WithMessage("Environment is required.")
            .MaximumLength(50).WithMessage("Environment cannot exceed 50 characters.");

        RuleFor(x => x.Title)
            .NotEmpty().WithMessage("Title is required.")
            .MaximumLength(250).WithMessage("Title cannot exceed 250 characters.");

        RuleFor(x => x.ExecutionDurationMs)
            .GreaterThanOrEqualTo(0)
            .When(x => x.ExecutionDurationMs.HasValue)
            .WithMessage("ExecutionDurationMs must be non-negative.");
    }
}
