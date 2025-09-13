namespace Transport.SharedKernel.Configuration;

public class ReserveOption : IReserveOption
{
    public int ReserveGenerationDays { get; set; }
    public int SlotLockTimeoutMinutes { get; set; } = 10;
    public int SlotLockCleanupIntervalMinutes { get; set; } = 1;
    public int MaxSimultaneousLocksPerUser { get; set; } = 5;
}
