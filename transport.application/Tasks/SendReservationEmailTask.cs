using Microsoft.EntityFrameworkCore;
using Transport.Business.Data;
using Transport.Business.Services.Email;
using Transport.Domain.Reserves;

namespace Transport.Business.Tasks;

public class SendReservationEmailTask : ISendReservationEmailTask
{
    private readonly IEmailSender _emailSender;
    private readonly IApplicationDbContext _applicationDbContext;

    public SendReservationEmailTask(
        IEmailSender emailSender, IApplicationDbContext applicationDbContext)
    {
        _emailSender = emailSender;
        _applicationDbContext = applicationDbContext;
    }

    public async Task ExecuteAsync(CustomerReserveCreatedEvent @event)
    {
//        var reserveCustomer = await _applicationDbContext.CustomerReserves.Where(p => p.CustomerId == @event.CustomerId
//            && p.ReserveId == @event.ReserveId)
//            .SingleOrDefaultAsync();

//        var mockEmails = new List<string>() { "agustinyuse@gmail.com", "francomaluendez2016@gmail.com" };

//        string emailToSend = reserveCustomer.CustomerEmail;

//        if (!mockEmails.Contains(reserveCustomer.CustomerEmail))
//        {
//            emailToSend = "francomaluendez2016@gmail.com";
//        }

//        var subject = $"Resumen de tu reserva #{reserveCustomer.CustomerReserveId}";
//        var body =
//$@"Hola {reserveCustomer.CustomerFullName},

//Gracias por tu reserva. Aquí tienes el resumen:

//- Reserva: #{reserveCustomer.ReserveId}
//- Origen: {reserveCustomer.OriginCityName}
//- Destino: {reserveCustomer.DestinationCityName}
//- Fecha de salida: {reserveCustomer.ReserveDate.ToString("G")}
//- Precio: {reserveCustomer.Price:C}

//¡Gracias por elegirnos!

//Equipo de Transporte";

//        await _emailSender.SendEmailAsync(emailToSend, subject, body);
    }
}
