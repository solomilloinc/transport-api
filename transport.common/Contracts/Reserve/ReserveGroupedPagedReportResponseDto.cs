namespace Transport.SharedKernel.Contracts.Reserve;

public class ReserveGroupedPagedReportResponseDto
{
    public PagedReportResponseDto<ReserveExternalReportResponseDto> Outbound { get; set; } = new();
    public PagedReportResponseDto<ReserveExternalReportResponseDto> Return { get; set; } = new();
}
