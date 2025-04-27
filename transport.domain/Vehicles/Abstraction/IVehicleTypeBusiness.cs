using Transport.SharedKernel.Contracts.Vehicle;
using Transport.SharedKernel;
using Transport.SharedKernel.Contracts.VehicleType;

namespace Transport.Domain.Vehicles.Abstraction;

public interface IVehicleTypeBusiness
{
    Task<Result<int>> Create(VehicleTypeCreateRequestDto dto);
    Task<Result<bool>> Delete(int vehicleTypeId);
    Task<Result<SharedKernel.PagedReportResponseDto<VehicleTypeReportResponseDto>>> GetVehicleTypeReport(PagedReportRequestDto<VehicleTypeReportFilterRequestDto> requestDto);
    Task<Result<bool>> Update(int vehicleTypeId, VehicleTypeUpdateRequestDto dto);
    Task<Result<bool>> UpdateStatus(int vehicleTypeId, EntityStatusEnum status);
}
