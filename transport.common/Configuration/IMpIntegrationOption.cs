namespace Transport.SharedKernel.Configuration;

public interface IMpIntegrationOption
{
    public string AccessToken { get; set; }
    public string WebhookSecret { get; set; }
    public string SuccessUrl { get; set; }
    public string FailureUrl { get; set; }
    public string PendingUrl { get; set; }
}
