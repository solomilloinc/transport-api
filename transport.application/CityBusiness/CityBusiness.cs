using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore;
using Transport.Business.Data;
using Transport.Domain.Cities;
using Transport.Domain.Cities.Abstraction;
using Transport.SharedKernel;
using Transport.SharedKernel.Contracts.City;

namespace Transport.Business.CityBusiness;

public class CityBusiness : ICityBusiness
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IApplicationDbContext _context;

    public CityBusiness(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result<int>> Create(CityCreateRequestDto dto)
    {
        if (_context.Cities.Any(x => x.Code == dto.Code || x.Name == dto.Name))
        {
            return Result.Failure<int>(CityError.CityAlreadyExist);
        }

        City city = new City
        {
            Code = dto.Code,
            Name = dto.Name
        };

        _context.Cities.Add(city);
        await _context.SaveChangesWithOutboxAsync();

        return city.CityId;
    }

    public async Task<Result<bool>> Delete(int cityId)
    {
        var city = await _context.Cities.FindAsync(cityId);

        if (city is null)
        {
            return Result.Failure<bool>(CityError.CityNotFound);
        }

        city.Status = EntityStatusEnum.Deleted;

        await _context.SaveChangesWithOutboxAsync();

        return Result.Success(true);
    }

    public async Task<Result<PagedReportResponseDto<CityReportResponseDto>>>
        GetReport(PagedReportRequestDto<CityReportFilterRequestDto> requestDto)
    {
        var query = _context.Cities
            .AsNoTracking()
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(requestDto.Filters?.Code))
            query = query.Where(v => v.Code.Contains(requestDto.Filters.Code));

        if (!string.IsNullOrWhiteSpace(requestDto.Filters?.Name))
            query = query.Where(v => v.Code.Contains(requestDto.Filters.Name));

        if (requestDto.Filters.Status is not null)
            query = query.Where(v => v.Status == requestDto.Filters.Status);

        var sortMappings = new Dictionary<string, Expression<Func<City, object>>>
        {
            ["code"] = v => v.Code,
            ["name"] = v => v.Name,
            ["status"] = v => v.Status
        };

        var pagedResult = await query.ToPagedReportAsync<CityReportResponseDto, City, CityReportFilterRequestDto>(
            requestDto,
            selector: v => new CityReportResponseDto(v.Name, v.Code),
            sortMappings: sortMappings
        );

        return Result.Success(pagedResult);
    }

    public async Task<Result<bool>> Update(int cityId, CityUpdateRequestDto dto)
    {
        var city = await _context.Cities
            .FindAsync(cityId);

        if (city is null)
        {
            return Result.Failure<bool>(CityError.CityNotFound);
        }

        city.Code = dto.Code;
        city.Name = dto.Name;

        await _context.SaveChangesWithOutboxAsync();
        return Result.Success(true);
    }

    public async Task<Result<bool>> UpdateStatus(int cityId, EntityStatusEnum status)
    {
        var city = await _context.Cities.FindAsync(cityId);
        if (city == null)
        {
            return Result.Failure<bool>(CityError.CityNotFound);
        }

        city.Status = status;

        await _context.SaveChangesWithOutboxAsync();

        return Result.Success(true);
    }
}

