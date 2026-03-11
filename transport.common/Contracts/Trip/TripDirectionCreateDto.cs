namespace Transport.SharedKernel.Contracts.Trip;

public record TripDirectionCreateDto(int TripId, int DirectionId, int Order, TimeSpan PickupTimeOffset);
