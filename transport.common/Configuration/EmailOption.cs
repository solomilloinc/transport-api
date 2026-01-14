namespace Transport.SharedKernel.Configuration;

public class EmailOption : IEmailOption
{
    public List<string> AllowedEmails { get; set; } = new();
    public string DevRedirectEmail { get; set; } = string.Empty;
    public bool IsProductionMode { get; set; } = false;
}

public interface IEmailOption
{
    List<string> AllowedEmails { get; set; }
    string DevRedirectEmail { get; set; }
    bool IsProductionMode { get; set; }
}
