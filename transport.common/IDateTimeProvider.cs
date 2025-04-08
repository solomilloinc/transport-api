namespace transport.common;

public interface IDateTimeProvider
{
    DateTime UtcNow { get; }
}
