namespace Transport.SharedKernel.Contracts.Driver;

public class DriverReportResponseDto
{
    public int DriverId { get; set; }
    public string FirstName { get; set; } = null!;
    public string LastName { get; set; } = null!;
    public string DocumentNumber { get; set; } = null!;
    public int VehicleInternalNumber { get; set; }
    public string Status { get; set; }

    public List<DriverReserveReportResponseDto> Reserves { get; set; } = new();
}
