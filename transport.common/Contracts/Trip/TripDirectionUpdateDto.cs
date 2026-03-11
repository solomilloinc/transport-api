namespace Transport.SharedKernel.Contracts.Trip;

public record TripDirectionUpdateDto(int DirectionId, int Order, TimeSpan PickupTimeOffset);
