namespace Transport.SharedKernel.Configuration;

public class ServiceBusSettings: IServiceBusSettings
{
    public string ConnectionString { get; set; } = string.Empty;
}
