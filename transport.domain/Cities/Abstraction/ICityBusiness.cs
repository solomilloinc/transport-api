using Transport.SharedKernel;
using Transport.SharedKernel.Contracts.City;

namespace Transport.Domain.Cities.Abstraction;

public interface ICityBusiness
{
    Task<Result<int>> Create(CityCreateRequestDto dto);
    Task<Result<SharedKernel.PagedReportResponseDto<CityReportResponseDto>>>
        GetReport(PagedReportRequestDto<CityReportFilterRequestDto> requestDto);
    Task<Result<bool>> Delete(int cityId);
    Task<Result<bool>> UpdateStatus(int cityId, EntityStatusEnum status);
    Task<Result<bool>> Update(int cityId, CityUpdateRequestDto dto);
}   
