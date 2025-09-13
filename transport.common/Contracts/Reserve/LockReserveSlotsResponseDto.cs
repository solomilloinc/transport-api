namespace Transport.SharedKernel.Contracts.Reserve;

public record LockReserveSlotsResponseDto(
    string LockToken,
    DateTime ExpiresAt,
    int TimeoutMinutes
);