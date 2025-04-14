namespace Transport.SharedKernel;

public interface IDateTimeProvider
{
    DateTime UtcNow { get; }
}
