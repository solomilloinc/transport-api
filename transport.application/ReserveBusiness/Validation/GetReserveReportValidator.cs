using FluentValidation;
using Transport.SharedKernel.Contracts.Reserve;

namespace Transport.Business.ReserveBusiness.Validation;

public class GetReserveReportValidator : AbstractValidator<ReserveReportFilterRequestDto>
{
    public GetReserveReportValidator()
    {
        RuleFor(x => x.OriginId)
            .GreaterThan(0).WithMessage("OriginId must be greater than zero.");

        RuleFor(x => x.DestinationId)
            .GreaterThan(0).WithMessage("DestinationId must be greater than zero.")
            .NotEqual(x => x.OriginId).WithMessage("DestinationId must be different from OriginId.");

        RuleFor(x => x.Passengers)
            .GreaterThan(0).WithMessage("Passengers must be greater than zero.");

        RuleFor(x => x.DepartureDate)
            .Must(date => date.Date >= DateTime.Today)
            .WithMessage("DepartureDate must be today or later.");

        When(x => x.ReturnDate.HasValue, () =>
        {
            RuleFor(x => x.ReturnDate.Value)
                .GreaterThanOrEqualTo(x => x.DepartureDate)
                .WithMessage("ReturnDate must be equal or after DepartureDate.");
        });
    }
}
