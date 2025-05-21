using System.Linq.Expressions;
using System.Linq;
using Microsoft.EntityFrameworkCore;

namespace Transport.SharedKernel;

public static class QueryableExtensions
{
    public static async Task<PagedReportResponseDto<TDto>> ToPagedReportAsync<TDto, TEntity, TFilter>(
        this IQueryable<TEntity> query,
        PagedReportRequestDto<TFilter> requestDto,
        Expression<Func<TEntity, TDto>> selector,
        Dictionary<string, Expression<Func<TEntity, object>>>? sortMappings = null)
    {
        if (!string.IsNullOrWhiteSpace(requestDto.SortBy) && sortMappings != null)
        {
            var sortKey = requestDto.SortBy.ToLower();
            if (sortMappings.TryGetValue(sortKey, out var sortExpression))
            {
                query = requestDto.SortDescending
                    ? query.OrderByDescending(sortExpression)
                    : query.OrderBy(sortExpression);
            }
        }

        var totalRecords = await query.CountAsync();

        var items = await query
            .Skip((requestDto.PageNumber - 1) * requestDto.PageSize)
            .Take(requestDto.PageSize)
            .Select(selector)
            .ToListAsync();

        return new PagedReportResponseDto<TDto>
        {
            PageNumber = requestDto.PageNumber,
            PageSize = requestDto.PageSize,
            TotalRecords = totalRecords,
            Items = items
        };
    }
}
