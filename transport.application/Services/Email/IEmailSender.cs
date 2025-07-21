﻿namespace Transport.Business.Services.Email;

public interface IEmailSender
{
    Task SendEmailAsync(string toEmail, string subject, string bodyHtml);
}
