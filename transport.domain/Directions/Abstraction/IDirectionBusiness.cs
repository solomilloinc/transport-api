using Transport.SharedKernel;
using Transport.SharedKernel.Contracts.Direction;

namespace Transport.Domain.Directions.Abstraction;

public interface IDirectionBusiness
{
    Task<Result<int>> CreateAsync(DirectionCreateDto dto);
    Task<Result<bool>> UpdateAsync(int directionId, DirectionUpdateDto dto);
    Task<Result<bool>> DeleteAsync(int directionId);
    Task<Result<PagedReportResponseDto<DirectionReportDto>>> GetReportAsync(PagedReportRequestDto<DirectionReportFilterRequestDto> requestDto);
}
