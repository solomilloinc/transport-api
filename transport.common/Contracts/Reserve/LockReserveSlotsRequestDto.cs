namespace Transport.SharedKernel.Contracts.Reserve;

public record LockReserveSlotsRequestDto(
    int OutboundReserveId,
    int? ReturnReserveId,
    int PassengerCount
);