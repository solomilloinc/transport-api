using System.Linq.Expressions;
using Transport.Business.Data;
using Transport.Domain.Vehicles;
using Transport.Domain.Vehicles.Abstraction;
using Transport.SharedKernel.Contracts.Vehicle;
using Transport.SharedKernel;
using Microsoft.EntityFrameworkCore;

namespace Transport.Business.VehicleBusiness;

public class VehicleBusiness : IVehicleBusiness
{
    private readonly IApplicationDbContext _context;

    public VehicleBusiness(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result<int>> Create(VehicleCreateRequestDto dto)
    {
        var vehicle = await _context.Vehicles
            .SingleOrDefaultAsync(x => x.InternalNumber == dto.InternalNumber);

        if (vehicle != null)
        {
            return Result.Failure<int>(VehicleError.VehicleAlreadyExists);
        }

        vehicle = new Vehicle
        {
            InternalNumber = dto.InternalNumber,
            VehicleTypeId = dto.VehicleTypeId,
        };

        _context.Vehicles.Add(vehicle);
        await _context.SaveChangesWithOutboxAsync();

        return vehicle.VehicleId;
    }

    public async Task<Result<bool>> Delete(int vehicleId)
    {
        var vehicle = _context.Vehicles
            .SingleOrDefault(x => x.VehicleId == vehicleId);

        if (vehicle is null)
        {
            return Result.Failure<bool>(VehicleError.VehicleNotFound);
        }

        vehicle.Status = EntityStatusEnum.Deleted;
        await _context.SaveChangesWithOutboxAsync();

        return Result.Success(true);
    }

    public async Task<Result<bool>> Update(int vehicleId, VehicleUpdateRequestDto dto)
    {
        var vehicle = _context.Vehicles
            .SingleOrDefault(x => x.VehicleId == vehicleId);

        if (vehicle is null)
        {
            return Result.Failure<bool>(VehicleError.VehicleNotFound);
        }

        vehicle.InternalNumber = dto.InternalNumber;
        vehicle.VehicleTypeId = dto.VehicleTypeId;

        await _context.SaveChangesWithOutboxAsync();
        return Result.Success(true);
    }

    public async Task<Result<bool>> UpdateStatus(int vehicleId, EntityStatusEnum status)
    {
        var vehicle = _context.Vehicles
            .SingleOrDefault(x => x.VehicleId == vehicleId);

        if (vehicle is null)
        {
            return Result.Failure<bool>(VehicleError.VehicleNotFound);
        }

        vehicle.Status = status;
        await _context.SaveChangesWithOutboxAsync();

        return Result.Success(true);
    }

    public async Task<Result<SharedKernel.PagedReportResponseDto<VehicleReportResponseDto>>> GetVehicleReport(PagedReportRequestDto<VehicleReportFilterRequestDto> requestDto)
    {
        var query = _context.Vehicles
            .AsNoTracking()
            .Include(v => v.VehicleType)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(requestDto.Filters?.InternalNumber))
            query = query.Where(v => v.InternalNumber.Contains(requestDto.Filters.InternalNumber));

        if (requestDto.Filters.VehicleTypeId is not null && requestDto.Filters.VehicleTypeId > 0)
            query = query.Where(v => v.VehicleTypeId == requestDto.Filters.VehicleTypeId);

        if (requestDto.Filters.status is not null)
            query = query.Where(v => v.Status == requestDto.Filters.status);

        var sortMappings = new Dictionary<string, Expression<Func<Vehicle, object>>>
        {
            ["internalnumber"] = v => v.InternalNumber,
            ["vehicletypeid"] = v => v.VehicleTypeId,
            ["status"] = v => v.Status
        };

        var pagedResult = await query.ToPagedReportAsync<VehicleReportResponseDto, Vehicle, VehicleReportFilterRequestDto>(
            requestDto,
            selector: v => new VehicleReportResponseDto(v.VehicleTypeId, v.VehicleTypeId, v.InternalNumber, v.VehicleType.Name, v.VehicleType.Quantity, v.VehicleType.ImageBase64),
            sortMappings: sortMappings
        );

        return Result.Success(pagedResult);
    }
}
