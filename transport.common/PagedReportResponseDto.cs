namespace Transport.SharedKernel;

public class PagedReportResponseDto<T>
{
    public List<T> Items { get; set; } = new();
    public int PageNumber { get; set; }
    public int PageSize { get; set; }
    public int TotalRecords { get; set; }

    public int TotalPages => (int)Math.Ceiling((double)TotalRecords / PageSize);

    public static PagedReportResponseDto<T> Create(List<T> allItems, int pageNumber, int pageSize)
    {
        var totalItems = allItems.Count;
        var pagedItems = allItems
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToList();

        return new PagedReportResponseDto<T>
        {
            Items = pagedItems,
            TotalRecords = totalItems,
            PageNumber = pageNumber,
            PageSize = pageSize
        };
    }
}
