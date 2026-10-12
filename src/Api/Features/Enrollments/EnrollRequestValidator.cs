using FluentValidation;

namespace Api.Features.Enrollments;

public sealed class EnrollRequestValidator : AbstractValidator<EnrollRequest>
{
    public static readonly string[] Programs = ["Morning cohort", "Evening cohort", "Weekend cohort"];

    public EnrollRequestValidator()
    {
        RuleFor(request => request.Name)
            .NotEmpty()
            .MaximumLength(100);

        RuleFor(request => request.Email)
            .NotEmpty()
            .EmailAddress()
            .MaximumLength(200);

        RuleFor(request => request.Program)
            .NotEmpty()
            .Must(program => Programs.Contains(program))
            .WithMessage("Program must be Morning cohort, Evening cohort, or Weekend cohort.");

        RuleFor(request => request.Processing)
            .NotEmpty()
            .Must(processing => processing is "immediate" or "delayed")
            .WithMessage("Processing must be immediate or delayed.");
    }
}
