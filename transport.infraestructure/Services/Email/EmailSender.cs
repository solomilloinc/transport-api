using FluentEmail.Core;
using Microsoft.Extensions.Logging;
using Transport.Business.Services.Email;
using Transport.SharedKernel.Configuration;

namespace Transport.Infraestructure.Services.Email;

public class EmailSender : IEmailSender
{
    private readonly IFluentEmail _fluentEmail;
    private readonly IEmailOption _emailOption;
    private readonly ILogger<EmailSender> _logger;

    public EmailSender(IFluentEmail fluentEmail, IEmailOption emailOption, ILogger<EmailSender> logger)
    {
        _fluentEmail = fluentEmail;
        _emailOption = emailOption;
        _logger = logger;
    }

    public async Task SendEmailAsync(string toEmail, string subject, string bodyHtml)
    {
        try
        {
            var (finalEmail, finalSubject) = ResolveEmailAndSubject(toEmail, subject);

            _logger.LogInformation(
                "Enviando email a {FinalEmail} (original: {OriginalEmail}) con asunto '{Subject}'",
                finalEmail, toEmail, finalSubject);

            var response = await _fluentEmail
                .To(finalEmail)
                .Subject(finalSubject)
                .Body(bodyHtml, true)
                .SendAsync();

            if (response.Successful)
            {
                _logger.LogInformation("Email enviado exitosamente a {FinalEmail}", finalEmail);
            }
            else
            {
                _logger.LogWarning(
                    "Email a {FinalEmail} completado con errores: {Errors}",
                    finalEmail, string.Join(", ", response.ErrorMessages));
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Excepcion al enviar email a {ToEmail}", toEmail);
            throw;
        }
    }

    private (string Email, string Subject) ResolveEmailAndSubject(string originalEmail, string originalSubject)
    {
        if (_emailOption.IsProductionMode)
        {
            return (originalEmail, originalSubject);
        }

        var isAllowed = _emailOption.AllowedEmails
            .Any(e => e.Equals(originalEmail, StringComparison.OrdinalIgnoreCase));

        if (isAllowed)
        {
            return (originalEmail, originalSubject);
        }

        var redirectEmail = _emailOption.DevRedirectEmail;
        var redirectSubject = $"[REDIRIGIDO de {originalEmail}] {originalSubject}";

        _logger.LogInformation(
            "Email redirigido: {OriginalEmail} -> {RedirectEmail} (modo desarrollo)",
            originalEmail, redirectEmail);

        return (redirectEmail, redirectSubject);
    }
}
