namespace Transport.SharedKernel.Configuration;

public class SmtpSettingOption: ISmtpSettingOption
{
    public string Host { get; set; }
    public int Port { get; set; }
    public string User { get; set; }
    public string Password { get; set; }
    public string FromEmail { get; set; }
    public string FromName { get; set; }
}
