namespace Transport.Domain.Passengers;

public enum PassengerStatusEnum
{
    // Estado inicial cuando se crea el pasajero pero aún no se confirmó el pago
    PendingPayment = 1,

    // Cuando el pago fue procesado y el pasajero tiene su lugar confirmado
    Confirmed = 2,

    // Si el pasajero o la reserva fue cancelada
    Cancelled = 3,

    // Cuando el pasajero efectivamente viajó (se marca después del viaje)
    Traveled = 4,

    // Si el pasajero confirmado no se presentó al viaje
    NoShow = 5,

    // Si hay un reembolso en proceso
    Refunded = 6
}