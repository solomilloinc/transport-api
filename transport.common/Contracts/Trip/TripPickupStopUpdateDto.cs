namespace Transport.SharedKernel.Contracts.Trip;

public record TripPickupStopUpdateDto(int DirectionId, int Order, TimeSpan PickupTimeOffset);
