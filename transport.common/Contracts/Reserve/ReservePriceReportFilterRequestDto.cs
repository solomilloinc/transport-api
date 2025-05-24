namespace Transport.SharedKernel.Contracts.Reserve;

public record ReservePriceReportFilterRequestDto(int? ReserveTypeId,  
    int? ServiceId,
    decimal? PriceFrom,
    decimal? PriceTo);
