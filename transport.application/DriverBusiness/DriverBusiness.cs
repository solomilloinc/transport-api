using Microsoft.EntityFrameworkCore;
using Transport.SharedKernel;
using Transport.Domain.Drivers;
using Transport.Business.Data;
using Transport.Domain.Drivers.Abstraction;
using Transport.SharedKernel.Contracts.Driver;
using System.Linq.Expressions;

namespace Transport.Business.DriverBusiness;

public class DriverBusiness : IDriverBusiness
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IApplicationDbContext _context;

    public DriverBusiness(IUnitOfWork unitOfWork, IApplicationDbContext context)
    {
        _unitOfWork = unitOfWork;
        _context = context;
    }

    public async Task<Result<int>> Create(DriverCreateRequestDto dto)
    {
        Driver driver = await _context.Drivers
            .SingleOrDefaultAsync(x => x.DocumentNumber == dto.documentNumber);

        if (driver != null)
        {
            return Result.Failure<int>(DriverError.DriverAlreadyExist);
        }

        if (dto.documentNumber.Contains("37976806"))
        {
            return Result.Failure<int>(DriverError.EmailInBlackList);
        }

        driver = new Driver
        {
            FirstName = dto.firstName,
            LastName = dto.lastName,
            DocumentNumber = dto.documentNumber
        };

        driver.Raise((new DriverCreatedEvent(driver.DriverId)));
        _context.Drivers.Add(driver);
        await _context.SaveChangesWithOutboxAsync();

        return driver.DriverId;
    }

    public async Task<Result<bool>> Delete(int driverId)
    {
        var driver = _context.Drivers
            .SingleOrDefault(x => x.DriverId == driverId);

        if (driver is null)
        {
            return Result.Failure<bool>(DriverError.DriverNotFound);
        }

        driver.Status = EntityStatusEnum.Deleted;

        await _context.SaveChangesWithOutboxAsync();
        return Result.Success(true);
    }

    public async Task<Result<SharedKernel.PagedReportResponseDto<DriverReportResponseDto>>>
    GetDriverReport(PagedReportRequestDto<DriverReportFilterRequestDto> requestDto)
    {
        var query = _context.Drivers
        .AsNoTracking()
        .Include(d => d.Reserves)
        .AsQueryable();

        if (!string.IsNullOrWhiteSpace(requestDto.Filters?.FirstName))
            query = query.Where(x => x.FirstName.Contains(requestDto.Filters.FirstName));

        if (!string.IsNullOrWhiteSpace(requestDto.Filters?.LastName))
            query = query.Where(x => x.LastName.Contains(requestDto.Filters.LastName));

        if (!string.IsNullOrWhiteSpace(requestDto.Filters?.DocumentNumber))
            query = query.Where(x => x.DocumentNumber.Contains(requestDto.Filters.DocumentNumber));

        var sortMappings = new Dictionary<string, Expression<Func<Driver, object>>>
        {
            ["firstname"] = d => d.FirstName,
            ["lastname"] = d => d.LastName,
            ["documentnumber"] = d => d.DocumentNumber
        };

        var pagedResult = await query.ToPagedReportAsync<DriverReportResponseDto, Driver, DriverReportFilterRequestDto>(
            requestDto,
            selector: d => new DriverReportResponseDto
            {
                DriverId = d.DriverId,
                FirstName = d.FirstName,
                LastName = d.LastName,
                DocumentNumber = d.DocumentNumber,
                Reserves = d.Reserves.Select(r => new DriverReserveReportResponseDto
                {
                    ReserveDate = r.ReserveDate,
                    Status = nameof(r.Status),
                    VehicleInternalNumber = r.Vehicle != null ? r.Vehicle.InternalNumber : null,
                }).ToList()
            },
            sortMappings: sortMappings
        );

        return Result.Success(pagedResult);
    }

    public async Task<Result<bool>> Update(int driverId, DriverUpdateRequestDto dto)
    {
        var driver = _context.Drivers
            .SingleOrDefault(x => x.DriverId == driverId);

        if (driver is null)
        {
            return Result.Failure<bool>(DriverError.DriverNotFound);
        }

        driver.FirstName = dto.FirstName;
        driver.LastName = dto.LastName;

        await _context.SaveChangesWithOutboxAsync();
        return Result.Success(true);
    }

    public async Task<Result<bool>> UpdateStatus(int driverId, EntityStatusEnum status)
    {
        var driver = _context.Drivers
            .SingleOrDefault(x => x.DriverId == driverId);

        if (driver is null)
        {
            return Result.Failure<bool>(DriverError.DriverNotFound);
        }

        driver.Status = status;

        await _context.SaveChangesWithOutboxAsync();
        return Result.Success(true);
    }
}
