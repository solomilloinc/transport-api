using Transport.SharedKernel;
using Transport.SharedKernel.Contracts;
using Transport.SharedKernel.Contracts.Driver;

namespace Transport.Domain.Drivers.Abstraction;

public interface IDriverBusiness
{
    Task<Result<int>> Create(DriverCreateRequestDto dto);
    Task<Result<SharedKernel.PagedReportResponseDto<DriverReportResponseDto>>> 
        GetDriverReport(PagedReportRequestDto<DriverReportFilterRequestDto> requestDto);
    Task<Result<bool>>Delete(int driverId);
    Task<Result<bool>> UpdateStatus(int driverId, EntityStatusEnum status);
    Task<Result<bool>> Update(int driverId, DriverUpdateRequestDto dto);
}
