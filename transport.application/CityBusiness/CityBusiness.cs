using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore;
using Transport.Business.Data;
using Transport.Domain.Cities;
using Transport.Domain.Cities.Abstraction;
using Transport.SharedKernel;
using Transport.SharedKernel.Contracts.City;
using Transport.Domain;
using Transport.Domain.Vehicles;

namespace Transport.Business.CityBusiness;

public class CityBusiness : ICityBusiness
{
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
            Name = dto.Name,
            Directions = dto.Directions?
            .Select(d => new Direction
            {
                Name = d.Name,
                Lat = d.Lat.GetValueOrDefault(),
                Lng = d.Lng.GetValueOrDefault()
            }).ToList() ?? new List<Direction>()
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

        if (requestDto.Filters.WithDirections)
        {
            query = query.Include(c => c.Directions.Any());
        }

        if (!string.IsNullOrWhiteSpace(requestDto.Filters?.Code))
            query = query.Where(v => v.Code.Contains(requestDto.Filters.Code));

        if (!string.IsNullOrWhiteSpace(requestDto.Filters?.Name))
            query = query.Where(v => v.Name.Contains(requestDto.Filters.Name));

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
            selector: v => new CityReportResponseDto(v.CityId, v.Name, v.Code, v.Directions.Select(d => new DirectionsReportDto(
                        d.DirectionId,
                        d.Name,
                        d.Lat,
                        d.Lng
                    )).ToList()),
            sortMappings: sortMappings
        );

        return Result.Success(pagedResult);
    }

    public async Task<Result<bool>> Update(int cityId, CityUpdateRequestDto dto)
    {
        var city = await _context.Cities
            .Include(c => c.Directions)
            .FirstOrDefaultAsync(c => c.CityId == cityId);

        if (city is null)
        {
            return Result.Failure<bool>(CityError.CityNotFound);
        }

        city.Code = dto.Code;
        city.Name = dto.Name;

        if (dto.Directions is not null)
        {
            city.Directions.Clear();

            foreach (var d in dto.Directions)
            {
                if (d.Lat.HasValue && d.Lng.HasValue)
                {
                    city.Directions.Add(new Direction
                    {
                        Name = d.Name,
                        Lat = d.Lat.Value,
                        Lng = d.Lng.Value
                    });
                }
            }
        }

        _context.Cities.Update(city);

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

        _context.Cities.Update(city);

        await _context.SaveChangesWithOutboxAsync();

        return Result.Success(true);
    }
}

