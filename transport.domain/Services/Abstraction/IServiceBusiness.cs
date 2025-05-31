using Transport.SharedKernel;
using Transport.SharedKernel.Contracts.Service;

namespace Transport.Domain.Services.Abstraction;

public interface IServiceBusiness
{
    Task<Result<int>> Create(ServiceCreateRequestDto requestDto);
    Task<Result<PagedReportResponseDto<ServiceReportResponseDto>>> GetServiceReport(PagedReportRequestDto<ServiceReportFilterRequestDto> requestDto);
    Task<Result<bool>> UpdateStatus(int serviceId, EntityStatusEnum status);
    Task<Result<bool>> Delete(int serviceId);
    Task<Result<bool>> Update(int serviceId, ServiceCreateRequestDto dto);
    Task<Result<bool>> GenerateFutureReservesAsync();
    Task<Result<bool>> UpdatePricesByPercentageAsync(PriceMassiveUpdateRequestDto requestDto);
    Task<Result<bool>> UpdatePrice(int serviceId, ServicePriceUpdateDto requestDto);
    Task<Result<bool>> AddPrice(int serviceId, ServicePriceAddDto requestDto);
    Task<Result<int>> CreateSchedule(int serviceId, ServiceScheduleCreateDto request);
    Task<Result<bool>> UpdateScheduleStatus(int scheduleId, EntityStatusEnum status);
    Task<Result<bool>> DeleteSchedule(int scheduleId);
    Task<Result<bool>> UpdateSchedule(int scheduleId, ServiceScheduleUpdateDto request);
    Task<Result<List<ServiceSchedule>>> GetSchedulesByServiceId(int serviceId);
}
