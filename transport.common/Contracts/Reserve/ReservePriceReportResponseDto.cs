namespace Transport.SharedKernel.Contracts.Reserve;

public record ReservePriceReportResponseDto(int ReservePriceId,
    int ServiceId,
    string ServiceName,
    decimal Price,
    int ReserveTypeId);
