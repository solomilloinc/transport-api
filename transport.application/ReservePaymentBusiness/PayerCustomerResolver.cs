using Microsoft.EntityFrameworkCore;
using Transport.Business.Data;
using Transport.Domain.Customers;
using Transport.Domain.Passengers;

namespace Transport.Business.ReservePaymentBusiness;

/// <summary>
/// Datos del pagador según MercadoPago. Verificado con un pago card real: Identification.Number y
/// Email vienen siempre; FirstName/LastName vienen null; Card.Cardholder.Name viene con el titular.
/// </summary>
internal record MercadoPagoPayerInfo(
    string? DocumentNumber,
    string? Email,
    string? FirstName,
    string? LastName,
    string? CardholderName);

/// <summary>
/// Resuelve-o-crea el Customer del PAGADOR de una compra online a partir del Payer de MercadoPago
/// (ADR 0008). Prioridad: (1) Customer existente con ese documento; (2) pasajero del booking con
/// ese documento (el que paga también viaja, ~90%: aporta el perfil completo); (3) tercero que no
/// viaja, materializado desde el Payer de MP con fallback de nombre al titular de la tarjeta.
/// Devuelve null si no hay documento o no se puede armar un perfil mínimo (sin nombre): en ese
/// caso el pago queda sin Customer y la compra no se asienta en cuenta corriente (ADR 0009).
/// </summary>
internal static class PayerCustomerResolver
{
    public static async Task<Customer?> ResolveOrCreateAsync(
        IApplicationDbContext context,
        MercadoPagoPayerInfo payer,
        IEnumerable<Passenger> bookingPassengers)
    {
        if (string.IsNullOrWhiteSpace(payer.DocumentNumber))
            return null;

        var byDocument = await context.Customers
            .SingleOrDefaultAsync(c => c.DocumentNumber == payer.DocumentNumber);
        if (byDocument is not null)
            return byDocument;

        var travelingPayer = bookingPassengers.FirstOrDefault(p => p.DocumentNumber == payer.DocumentNumber);

        string? firstName, lastName, phone, email;
        if (travelingPayer is not null)
        {
            firstName = travelingPayer.FirstName;
            lastName = travelingPayer.LastName;
            phone = travelingPayer.Phone;
            email = travelingPayer.Email ?? payer.Email;
        }
        else
        {
            // Tercero: MP no manda FirstName/LastName en el Payer (verificado), así que el nombre
            // cae al titular de la tarjeta. En wallet sin tarjeta puede no haber nombre → null.
            firstName = payer.FirstName;
            lastName = payer.LastName;
            if (string.IsNullOrWhiteSpace(firstName) && !string.IsNullOrWhiteSpace(payer.CardholderName))
            {
                var parts = payer.CardholderName.Trim().Split(' ', 2);
                firstName = parts[0];
                lastName = parts.Length > 1 ? parts[1] : parts[0];
            }
            phone = null;
            email = payer.Email;
        }

        if (string.IsNullOrWhiteSpace(firstName))
            return null;

        // Email único por tenant: si ya pertenece a un cliente se atribuye a ese cliente en lugar
        // de crear un duplicado que violaría el índice (TenantId, Email). Si no hay email, solo se
        // puede crear si no existe ya otro Customer con email vacío (mismo índice).
        if (!string.IsNullOrWhiteSpace(email))
        {
            var byEmail = await context.Customers.FirstOrDefaultAsync(c => c.Email == email);
            if (byEmail is not null)
                return byEmail;
        }
        else if (await context.Customers.AnyAsync(c => c.Email == string.Empty))
        {
            return null;
        }

        var customer = new Customer
        {
            FirstName = firstName,
            LastName = string.IsNullOrWhiteSpace(lastName) ? firstName : lastName,
            DocumentNumber = payer.DocumentNumber,
            Email = email ?? string.Empty,
            Phone1 = phone ?? string.Empty,
        };
        context.Customers.Add(customer);
        await context.SaveChangesWithOutboxAsync();
        return customer;
    }
}
