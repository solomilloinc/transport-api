using FluentEmail.Core;
using Microsoft.Extensions.Logging;
using Transport.Business.Services.Email;
using Transport.SharedKernel.Configuration;

namespace Transport.Infraestructure.Services.Email;

public class EmailSender : IEmailSender
{
    private readonly IFluentEmail _fluentEmail;
    private readonly ISmtpSettingOption _smtpSetting;
    private readonly ILogger<EmailSender> _logger;

    public EmailSender(IFluentEmail fluentEmail, ISmtpSettingOption smtpSetting, ILogger<EmailSender> logger)
    {
        _fluentEmail = fluentEmail;
        _smtpSetting = smtpSetting;
        _logger = logger;
    }

    public async Task SendEmailAsync(string toEmail, string subject, string bodyHtml)
    {
        try
        {
            _logger.LogInformation("Intentando enviar email a {ToEmail} con asunto '{Subject}'", toEmail, subject);

            // Simulación o descomentar para envío real:
            // var response = await _fluentEmail
            //     .To(toEmail)
            //     .Subject(subject)
            //     .Body(bodyHtml, true)
            //     .SendAsync();

            // Simulación mientras lo estás debuggeando

            _logger.LogInformation("Email enviado exitosamente a {ToEmail}", toEmail);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Excepción al enviar email a {ToEmail}", toEmail);
            throw;
        }
    }
}
