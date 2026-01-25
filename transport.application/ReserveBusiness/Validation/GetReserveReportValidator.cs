using FluentValidation;
using Transport.SharedKernel.Contracts.Reserve;

namespace Transport.Business.ReserveBusiness.Validation;

public class GetReserveReportValidator : AbstractValidator<ReserveReportFilterRequestDto>
{
    public GetReserveReportValidator()
    {
        RuleFor(x => x.TripId)
            .GreaterThan(0).WithMessage("TripId must be greater than zero.");

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
