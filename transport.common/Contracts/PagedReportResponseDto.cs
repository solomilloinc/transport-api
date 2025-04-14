namespace Transport.SharedKernel.Contracts;

public class PagedReportResponseDto<T>
{
    public List<T> Items { get; set; } = new();
    public int PageNumber { get; set; }
    public int PageSize { get; set; }
    public int TotalRecords { get; set; }

    public int TotalPages => (int)Math.Ceiling((double)TotalRecords / PageSize);
}

