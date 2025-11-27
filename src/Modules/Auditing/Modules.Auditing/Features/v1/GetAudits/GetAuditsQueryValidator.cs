using FluentValidation;
using FSH.Modules.Auditing.Contracts.v1.GetAudits;

namespace FSH.Modules.Auditing.Features.v1.GetAudits;

public sealed class GetAuditsQueryValidator : AbstractValidator<GetAuditsQuery>
{
    public GetAuditsQueryValidator()
    {
        RuleFor(q => q.PageNumber)
            .GreaterThan(0)
            .When(q => q.PageNumber.HasValue);

        RuleFor(q => q.PageSize)
            .InclusiveBetween(1, 100)
            .When(q => q.PageSize.HasValue);

        RuleFor(q => q)
            .Must(q => !q.FromUtc.HasValue || !q.ToUtc.HasValue || q.FromUtc <= q.ToUtc)
            .WithMessage("FromUtc must be less than or equal to ToUtc.");
    }
}

