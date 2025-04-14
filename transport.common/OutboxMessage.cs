namespace Transport.SharedKernel;

public class OutboxMessage
{
    public Guid Id { get; set; }
    public DateTime OccurredOn { get; set; }
    public string Type { get; set; } = default!;
    public string Content { get; set; } = default!;
    public string? Topic { get; set; }
    public bool Processed { get; set; } = false;
    public DateTime? ProcessedOn { get; set; }
}

