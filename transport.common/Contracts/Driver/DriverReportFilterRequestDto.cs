namespace Transport.SharedKernel.Contracts.Driver;

public class DriverReportFilterRequestDto
{
    public int DriverId { get; set; }
    public string FirstName { get; set; } = null!;
    public string LastName { get; set; } = null!;
    public string DocumentNumber { get; set; } = null!;
}
