namespace Transport.SharedKernel.Contracts.Trip;

public record TripPickupStopCreateDto(int TripId, int DirectionId, int Order, TimeSpan PickupTimeOffset);
