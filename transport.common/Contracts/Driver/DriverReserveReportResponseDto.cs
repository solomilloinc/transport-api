
namespace Transport.SharedKernel.Contracts.Driver;

public class DriverReserveReportResponseDto
{
    public DateTime ReserveDate { get; set; }
    public string Status { get; set; }
    public string? VehicleInternalNumber { get; set; }
}
