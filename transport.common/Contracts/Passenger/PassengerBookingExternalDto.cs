namespace Transport.SharedKernel.Contracts.Passenger;

public record PassengerBookingExternalDto(
    int? CustomerId,
    bool IsPayment,
    bool HasTraveled,
    string FirstName,
    string LastName,
    string? Email,
    string Phone1,
    string DocumentNumber,
    LegInfoDto Outbound,
    LegInfoDto? Return);
