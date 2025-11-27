using FluentValidation;
using FSH.Modules.Identity.Contracts.v1.Tokens.RefreshToken;

namespace FSH.Modules.Identity.Features.v1.Tokens.RefreshToken;

public class RefreshTokenCommandValidator : AbstractValidator<RefreshTokenCommand>
{
    public RefreshTokenCommandValidator()
    {
        RuleFor(p => p.Token)
            .Cascade(CascadeMode.Stop)
            .NotEmpty();

        RuleFor(p => p.RefreshToken)
            .Cascade(CascadeMode.Stop)
            .NotEmpty();
    }
}

