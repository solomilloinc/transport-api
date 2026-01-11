using Transport.SharedKernel;
using Transport.SharedKernel.Contracts.CashBox;

namespace Transport.Domain.CashBoxes.Abstraction;

public interface ICashBoxBusiness
{
    Task<Result<CashBoxResponseDto>> CloseCashBox(CloseCashBoxRequestDto request);
    Task<Result<CashBoxResponseDto>> GetCurrentCashBox();
    Task<Result<CashBox>> GetOpenCashBoxEntity();
    Task<Result<PagedReportResponseDto<CashBoxResponseDto>>> GetCashBoxReport(
        PagedReportRequestDto<CashBoxReportFilterRequestDto> requestDto);
}
