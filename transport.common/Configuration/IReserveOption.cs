namespace Transport.SharedKernel.Configuration;

public interface IReserveOption
{
    public int ReserveGenerationDays { get; set; }
    public int SlotLockTimeoutMinutes { get; set; }
    public int SlotLockCleanupIntervalMinutes { get; set; }
    public int MaxSimultaneousLocksPerUser { get; set; }
}
