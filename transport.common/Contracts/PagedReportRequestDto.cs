namespace Transport.SharedKernel.Contracts;

public class PagedReportRequestDto<TFilter>
{
    public int PageNumber { get; set; } = 1;
    public int PageSize { get; set; } = 50;
    public string? SortBy { get; set; }
    public bool SortDescending { get; set; } = false;

    public TFilter? Filters { get; set; }
}
