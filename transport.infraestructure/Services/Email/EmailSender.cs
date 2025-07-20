using Transport.Business.Services.Email;
using FluentEmail.Core;
using Transport.SharedKernel.Configuration;

namespace Transport.Infraestructure.Services.Email;

public class EmailSender : IEmailSender
{
    private readonly IFluentEmail _fluentEmail;
    private readonly ISmtpSetting _smtpSetting;
    public EmailSender(IFluentEmail fluentEmail, ISmtpSetting smtpSetting)
    {
        _fluentEmail = fluentEmail;
        _smtpSetting = smtpSetting;
    }
    public async Task SendEmailAsync(string toEmail, string subject, string bodyHtml)
    {
        var response = await _fluentEmail
              .SetFrom(_smtpSetting.FromEmail, _smtpSetting.FromName)
              .To(toEmail)
              .Subject(subject)
              .Body(bodyHtml, true)
              .SendAsync();

        if (!response.Successful)
            throw new Exception($"Email failed: {string.Join(", ", response.ErrorMessages)}");
    }
}
