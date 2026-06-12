using FluentValidation;
using Transport.SharedKernel.Contracts.User;

namespace Transport.Business.UserBusiness.Validation;

public class UserUpdateRequestValidator : AbstractValidator<UserUpdateRequestDto>
{
    public UserUpdateRequestValidator()
    {
        RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(150);
        RuleFor(x => x.Role).NotEmpty().MaximumLength(20);
        RuleFor(x => x.Status).IsInEnum();
    }
}
