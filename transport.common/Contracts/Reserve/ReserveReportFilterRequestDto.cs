namespace Transport.SharedKernel.Contracts.Reserve;

public record ReserveReportFilterRequestDto(int? ReserveTypeId,  
    int? ServiceId,
    decimal? PriceFrom,
    decimal? PriceTo);
