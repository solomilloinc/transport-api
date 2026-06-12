using FluentValidation;
using Transport.SharedKernel.Contracts.User;

namespace Transport.Business.UserBusiness.Validation;

public class ClientProfileCompleteRequestValidator : AbstractValidator<ClientProfileCompleteRequestDto>
{
    public ClientProfileCompleteRequestValidator()
    {
        RuleFor(x => x.FirstName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.LastName).NotEmpty().MaximumLength(100);
        RuleFor(x => x.DocumentNumber).NotEmpty().MaximumLength(20);
        RuleFor(x => x.Phone1).NotEmpty().MaximumLength(20);
        RuleFor(x => x.Phone2).MaximumLength(20);
    }
}
