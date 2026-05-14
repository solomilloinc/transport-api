namespace Transport.SharedKernel.Contracts.Passenger;

public record PassengerBookingDto(
    int CustomerId,
    bool IsPayment,
    bool HasTraveled,
    LegInfoDto Outbound,
    LegInfoDto? Return);
