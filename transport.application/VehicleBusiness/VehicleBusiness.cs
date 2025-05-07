using System.Linq.Expressions;
using Transport.Business.Data;
using Transport.Domain.Vehicles;
using Transport.Domain.Vehicles.Abstraction;
using Transport.SharedKernel.Contracts.Vehicle;
using Transport.SharedKernel;
using Microsoft.EntityFrameworkCore;
using Transport.Domain.Services;

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

        if (dto.VehicleTypeId is null)
        {
            return Result.Failure<int>(VehicleTypeError.VehicleTypeNotFound);
        }

        var vehicleType = await _context.VehicleTypes.SingleOrDefaultAsync(x => x.VehicleTypeId == dto.VehicleTypeId);

        if (vehicleType is null)
        {
            return Result.Failure<int>(VehicleTypeError.VehicleTypeNotFound);
        }

        if (vehicleType.Quantity < dto.AvailableQuantity)
        {
            return Result.Failure<int>(VehicleError.VehicleAvailableQuantityNotValid);
        }

        vehicle = new Vehicle
        {
            InternalNumber = dto.InternalNumber,
            VehicleTypeId = dto.VehicleTypeId.Value,
        };

        _context.Vehicles.Add(vehicle);
        await _context.SaveChangesWithOutboxAsync();

        return vehicle.VehicleId;
    }

    public async Task<Result<bool>> Delete(int vehicleId)
    {
        var vehicle = await _context.Vehicles.FindAsync(vehicleId);

        if (vehicle is null)
        {
            return Result.Failure<bool>(VehicleError.VehicleNotFound);
        }

        vehicle.Status = EntityStatusEnum.Deleted;

        _context.Vehicles.Update(vehicle);

        await _context.SaveChangesWithOutboxAsync();

        return Result.Success(true);
    }

    public async Task<Result<bool>> Update(int vehicleId, VehicleUpdateRequestDto dto)
    {
        var vehicle = await _context.Vehicles.FindAsync(vehicleId);

        if (vehicle is null)
        {
            return Result.Failure<bool>(VehicleError.VehicleNotFound);
        }

        var vehicleType = await _context.VehicleTypes.FindAsync(dto.VehicleTypeId);

        if (vehicleType is null)
        {
            return Result.Failure<bool>(VehicleTypeError.VehicleTypeNotFound);
        }

        if (vehicleType.Quantity < dto.AvailableQuantity)
        {
            return Result.Failure<bool>(VehicleError.VehicleAvailableQuantityNotValid);
        }

        vehicle.InternalNumber = dto.InternalNumber;
        vehicle.VehicleTypeId = dto.VehicleTypeId;
        vehicle.AvailableQuantity = dto.AvailableQuantity;

        _context.Vehicles.Update(vehicle);

        await _context.SaveChangesWithOutboxAsync();
        return Result.Success(true);
    }

    public async Task<Result<bool>> UpdateStatus(int vehicleId, EntityStatusEnum status)
    {
        var vehicle = await _context.Vehicles.FindAsync(vehicleId);

        if (vehicle is null)
        {
            return Result.Failure<bool>(VehicleError.VehicleNotFound);
        }

        vehicle.Status = status;

        _context.Vehicles.Update(vehicle);

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
            selector: v => new VehicleReportResponseDto(v.VehicleId, v.VehicleTypeId, v.InternalNumber, v.VehicleType.Name, v.VehicleType.Quantity, v.VehicleType.ImageBase64, v.Status.ToString(), v.AvailableQuantity),
            sortMappings: sortMappings
        );

        return Result.Success(pagedResult);
    }
}
