using FluentValidation;
using Transport.SharedKernel.Contracts.User;

namespace Transport.Business.UserBusiness.Validation;

public class GoogleLoginRequestValidator : AbstractValidator<GoogleLoginRequestDto>
{
    public GoogleLoginRequestValidator()
    {
        RuleFor(x => x.IdToken).NotEmpty().MaximumLength(4096);
    }
}
