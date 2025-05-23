namespace Transport.SharedKernel.Contracts.Reserve;

public record ReserveReportResponseDto(int ReservePriceId,
    int ServiceId,
    string ServiceName,
    decimal Price,
    int ReserveTypeId);
