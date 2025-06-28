namespace Transport.SharedKernel.Contracts.Reserve;

public record CustomerReserveReportResponseDto(
    int CustomerReserveId,
    int CustomerId,
    string FullName,
    string DocumentNumber,
    string Email,
    string FullPhone,
    int ReserveId,
    int DropoffLocationId,
    string? DropoffLocationName,
    int PickupLocationId,
    string? PickupLocationName);
