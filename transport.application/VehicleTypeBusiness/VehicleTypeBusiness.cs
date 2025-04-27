using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Transport.Business.Data;
using Transport.Domain.Vehicles;
using Transport.Domain.Vehicles.Abstraction;
using Transport.SharedKernel;
using Transport.SharedKernel.Contracts.Vehicle;
using Transport.SharedKernel.Contracts.VehicleType;

namespace Transport.Business.VehicleTypeBusiness;

public class VehicleTypeBusiness : IVehicleTypeBusiness
{
    private readonly IApplicationDbContext _context;

    public VehicleTypeBusiness(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result<int>> Create(VehicleTypeCreateRequestDto dto)
    {
        var vehicleType = await _context.VehicleTypes
          .SingleOrDefaultAsync(x => x.Name == dto.Name);

        if (vehicleType != null)
        {
            return Result.Failure<int>(VehicleError.VehicleAlreadyExists);
        }

        vehicleType = new VehicleType
        {
            Name = dto.Name,
            ImageBase64 = dto.ImageBase64,
            Quantity = dto.Quantity,
            Status = EntityStatusEnum.Active
        };

        if (dto.Vehicles is not null && dto.Vehicles.Any())
        {
            vehicleType.Vehicles = dto.Vehicles.Select(p => new Vehicle()
            { 
                VehicleTypeId = vehicleType.VehicleTypeId, 
                InternalNumber = p.InternalNumber,                
            }).ToList();
        }

        _context.VehicleTypes.Add(vehicleType);
        await _context.SaveChangesWithOutboxAsync();

        return vehicleType.VehicleTypeId;
    }

    public async Task<Result<bool>> Delete(int vehicleTypeId)
    {
        var vehicleType = await _context.VehicleTypes.FindAsync(vehicleTypeId);

        if (vehicleType is null)
        {
            return Result.Failure<bool>(VehicleError.VehicleNotFound);
        }

        vehicleType.Status = EntityStatusEnum.Deleted;
        await _context.SaveChangesWithOutboxAsync();

        return Result.Success(true);
    }

    public async Task<Result<PagedReportResponseDto<VehicleTypeReportResponseDto>>> GetVehicleTypeReport(PagedReportRequestDto<VehicleTypeReportFilterRequestDto> requestDto)
    {
        var query = _context.VehicleTypes
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(requestDto.Filters?.Name))
            query = query.Where(v => v.Name.Contains(requestDto.Filters.Name));

        if (requestDto.Filters.VehicleTypeId is not null)
            query = query.Where(v => v.VehicleTypeId == requestDto.Filters.VehicleTypeId);

        var sortMappings = new Dictionary<string, Expression<Func<VehicleType, object>>>
        {
            ["vehicletypeid"] = v => v.VehicleTypeId,
            ["name"] = v => v.Name
        };

        var pagedResult = await query.ToPagedReportAsync<VehicleTypeReportResponseDto, VehicleType, VehicleTypeReportFilterRequestDto>(
            requestDto,
            selector: v => new VehicleTypeReportResponseDto(v.VehicleTypeId, v.Name, v.ImageBase64, v.Quantity),
            sortMappings: sortMappings
        );

        return Result.Success(pagedResult);
    }

    public async Task<Result<bool>> Update(int vehicleTypeId, VehicleTypeUpdateRequestDto dto)
    {
        var vehicleType = await _context.VehicleTypes.FindAsync(vehicleTypeId);

        if (vehicleType is null)
        {
            return Result.Failure<bool>(VehicleError.VehicleNotFound);
        }

        vehicleType.Name = dto.Name;

        await _context.SaveChangesWithOutboxAsync();
        return Result.Success(true);
    }

    public async Task<Result<bool>> UpdateStatus(int vehicleTypeId, EntityStatusEnum status)
    {
        var vehicleType = await _context.VehicleTypes.FindAsync(vehicleTypeId);

        if (vehicleType is null)
        {
            return Result.Failure<bool>(VehicleError.VehicleNotFound);
        }

        vehicleType.Status = status;
        await _context.SaveChangesWithOutboxAsync();

        return Result.Success(true);
    }
}
