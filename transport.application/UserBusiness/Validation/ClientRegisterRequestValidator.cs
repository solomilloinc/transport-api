using FluentValidation;
using Transport.SharedKernel.Contracts.User;

namespace Transport.Business.UserBusiness.Validation;

public class ClientRegisterRequestValidator : AbstractValidator<ClientRegisterRequestDto>
{
    public ClientRegisterRequestValidator()
    {
        RuleFor(x => x.FirstName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.LastName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.Email).NotEmpty().EmailAddress().MaximumLength(150);
        RuleFor(x => x.DocumentNumber).NotEmpty().MaximumLength(20);
        RuleFor(x => x.Phone1).NotEmpty().MaximumLength(20);
        RuleFor(x => x.Phone2).MaximumLength(20);
        RuleFor(x => x.Password).NotEmpty().MinimumLength(8).MaximumLength(100);
    }
}
