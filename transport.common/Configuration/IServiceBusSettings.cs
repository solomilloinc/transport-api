namespace Transport.SharedKernel.Configuration;

public interface IServiceBusSettings
{
    string ConnectionString { get; set; }
}
