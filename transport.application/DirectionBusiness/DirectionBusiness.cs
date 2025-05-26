using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using Transport.Business.Data;
using Transport.Domain;
using Transport.Domain.Directions;
using Transport.Domain.Directions.Abstraction;
using Transport.SharedKernel;
using Transport.SharedKernel.Contracts.Direction;

namespace Transport.Business.DirectionBusiness;

public class DirectionBusiness : IDirectionBusiness
{
    private readonly IApplicationDbContext _context;

    public DirectionBusiness(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result<int>> CreateAsync(DirectionCreateDto dto)
    {
        var direction = new Direction
        {
            Name = dto.Name,
            Lat = dto.Lat,
            Lng = dto.Lng,
            CityId = dto.CityId
        };

        _context.Directions.Add(direction);
        await _context.SaveChangesWithOutboxAsync();
        return direction.DirectionId;
    }

    public async Task<Result<bool>> UpdateAsync(int directionId, DirectionUpdateDto dto)
    {
        var direction = await _context.Directions.FindAsync(directionId);
        if (direction == null) return Result.Failure<bool>(DirectionError.DirectionNotFound);

        direction.Name = dto.Name;
        direction.Lat = dto.Lat;
        direction.Lng = dto.Lng;
        direction.CityId = dto.CityId;

        _context.Directions.Update(direction);
        await _context.SaveChangesWithOutboxAsync();
        return true;
    }

    public async Task<Result<bool>> DeleteAsync(int directionId)
    {
        var direction = await _context.Directions.FindAsync(directionId);
        if (direction == null) return Result.Failure<bool>(DirectionError.DirectionNotFound);

        direction.Status = SharedKernel.EntityStatusEnum.Deleted;
        _context.Directions.Update(direction);
        await _context.SaveChangesWithOutboxAsync();
        return true;
    }

    public async Task<Result<PagedReportResponseDto<DirectionReportDto>>> GetReportAsync(PagedReportRequestDto<DirectionReportFilterRequestDto> requestDto)
    {
        var query = _context.Directions
             .AsNoTracking()
             .Include(d => d.City)
             .AsQueryable();

        if (!string.IsNullOrWhiteSpace(requestDto.Filters?.DirectionName))
        {
            query = query.Where(d => d.Name.Contains(requestDto.Filters.DirectionName));
        }

        if (requestDto.Filters?.CityId is not null)
        {
            query = query.Where(d => d.CityId == requestDto.Filters.CityId.Value);
        }

        var sortMappings = new Dictionary<string, Expression<Func<Direction, object>>>
        {
            ["name"] = d => d.Name,
            ["cityName"] = d => d.City.Name,
            ["createdDate"] = d => d.CreatedDate
        };

        var pagedResult = await query.ToPagedReportAsync<DirectionReportDto, Direction, DirectionReportFilterRequestDto>(
            requestDto,
            selector: d => new DirectionReportDto(
                d.DirectionId,
                d.Name,
                d.Lat,
                d.Lng,
                d.CityId,
                d.City.Name,
                d.CreatedDate,
                d.UpdatedDate
            ),
            sortMappings: sortMappings
        );

        return Result.Success(pagedResult);
    }
}