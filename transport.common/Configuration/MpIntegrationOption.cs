namespace Transport.SharedKernel.Configuration;

public class MpIntegrationOption : IMpIntegrationOption
{
    public string AccessToken { get; set; }
    public string WebhookSecret { get; set; }
    public string SuccessUrl { get; set; }
    public string PendingUrl { get; set; }
    public string FailureUrl { get; set; }

}
