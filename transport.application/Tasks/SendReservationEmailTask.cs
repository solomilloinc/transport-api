using Transport.Business.Services.Email;
using Transport.Domain.Reserves;

namespace Transport.Business.Tasks;

public class SendReservationEmailTask : ISendReservationEmailTask
{
    private readonly IEmailSender _emailSender;

    public SendReservationEmailTask(IEmailSender emailSender)
    {
        _emailSender = emailSender;
    }

    public async Task ExecuteAsync(CustomerReserveCreatedEvent @event)
    {
        if (string.IsNullOrWhiteSpace(@event.CustomerEmail))
            return;

        var subject = $"Confirmacion de tu reserva #{@event.ReserveId}";
        var body = BuildEmailBody(@event);

        await _emailSender.SendEmailAsync(@event.CustomerEmail, subject, body);
    }

    private static string BuildEmailBody(CustomerReserveCreatedEvent @event)
    {
        var departureTime = @event.DepartureHour.ToString(@"hh\:mm");
        var reserveDate = @event.ReserveDate.ToString("dd/MM/yyyy");

        return $@"
<!DOCTYPE html>
<html>
<head>
    <meta charset='utf-8'>
    <style>
        body {{ font-family: Arial, sans-serif; line-height: 1.6; color: #333; }}
        .container {{ max-width: 600px; margin: 0 auto; padding: 20px; }}
        .header {{ background-color: #2563eb; color: white; padding: 20px; text-align: center; border-radius: 8px 8px 0 0; }}
        .content {{ background-color: #f8fafc; padding: 20px; border: 1px solid #e2e8f0; }}
        .details {{ background-color: white; padding: 15px; border-radius: 8px; margin: 15px 0; }}
        .details-row {{ display: flex; justify-content: space-between; padding: 8px 0; border-bottom: 1px solid #e2e8f0; }}
        .details-row:last-child {{ border-bottom: none; }}
        .label {{ color: #64748b; font-weight: 500; }}
        .value {{ color: #1e293b; font-weight: 600; }}
        .footer {{ text-align: center; padding: 20px; color: #64748b; font-size: 12px; }}
        .price {{ font-size: 24px; color: #059669; font-weight: bold; }}
    </style>
</head>
<body>
    <div class='container'>
        <div class='header'>
            <h1>Reserva Confirmada</h1>
            <p>Reserva #{@event.ReserveId}</p>
        </div>
        <div class='content'>
            <p>Hola <strong>{@event.CustomerFullName}</strong>,</p>
            <p>Tu reserva ha sido confirmada exitosamente. Aqui tienes los detalles:</p>

            <div class='details'>
                <div class='details-row'>
                    <span class='label'>Servicio:</span>
                    <span class='value'>{@event.ServiceName}</span>
                </div>
                <div class='details-row'>
                    <span class='label'>Origen:</span>
                    <span class='value'>{@event.OriginName}</span>
                </div>
                <div class='details-row'>
                    <span class='label'>Destino:</span>
                    <span class='value'>{@event.DestinationName}</span>
                </div>
                <div class='details-row'>
                    <span class='label'>Fecha:</span>
                    <span class='value'>{reserveDate}</span>
                </div>
                <div class='details-row'>
                    <span class='label'>Hora de salida:</span>
                    <span class='value'>{departureTime}</span>
                </div>
                <div class='details-row'>
                    <span class='label'>Total:</span>
                    <span class='value price'>${@event.TotalPrice:N2}</span>
                </div>
            </div>

            <p>Gracias por elegirnos. Te esperamos!</p>
        </div>
        <div class='footer'>
            <p>Este es un correo automatico, por favor no responder.</p>
            <p>Equipo de Transporte</p>
        </div>
    </div>
</body>
</html>";
    }
}
