using Transport.SharedKernel;
using Transport.SharedKernel.Contracts.Vehicle;

namespace Transport.Domain.Vehicles.Abstraction;

public interface IVehicleBusiness
{
    Task<Result<int>> Create(VehicleCreateRequestDto dto);
    Task<Result<bool>> Delete(int vehicleId);
    Task<Result<SharedKernel.PagedReportResponseDto<VehicleReportResponseDto>>> GetVehicleReport(PagedReportRequestDto<VehicleReportFilterRequestDto> requestDto);
    Task<Result<bool>> Update(int vehicleId, VehicleUpdateRequestDto dto);
    Task<Result<bool>> UpdateStatus(int vehicleId, EntityStatusEnum status);
}
