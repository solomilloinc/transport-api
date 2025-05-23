﻿using FluentValidation;
using Transport.SharedKernel.Contracts.City;

namespace Transport.Business.CityBusiness.Validation;

internal class CityUpdateRequestValidator : AbstractValidator<CityUpdateRequestDto>
{
    public CityUpdateRequestValidator()
    {
        RuleFor(p => p.Name)
                    .NotEmpty()
                    .WithMessage("Name is required")
                    .MinimumLength(2)
                    .WithMessage("Name must be at least 2 characters long")
                    .MaximumLength(100)
                    .WithMessage("Name must not exceed 50 characters");
    }
}
