using Transport.SharedKernel;

namespace Transport.Domain.Reserves;

public static class ReserveSlotLockError
{
    public static readonly Error InsufficientSlots = Error.Validation(
        "ReserveSlotLock.InsufficientSlots",
        "No hay suficientes cupos disponibles para realizar la reserva.");

    public static readonly Error InvalidOrExpiredLock = Error.Validation(
        "ReserveSlotLock.InvalidOrExpiredLock",
        "El token de bloqueo es inválido o ha expirado. Inicie el proceso de reserva nuevamente.");

    public static readonly Error LockReserveMismatch = Error.Validation(
        "ReserveSlotLock.LockReserveMismatch",
        "Las reservas solicitadas no coinciden con las del bloqueo.");

    public static readonly Error LockAlreadyUsed = Error.Validation(
        "ReserveSlotLock.LockAlreadyUsed",
        "Este bloqueo ya fue utilizado para crear una reserva.");

    public static readonly Error LockNotFound = Error.NotFound(
        "ReserveSlotLock.LockNotFound",
        "No se encontró el bloqueo especificado.");

    public static readonly Error MaxSimultaneousLocksExceeded = Error.Validation(
        "ReserveSlotLock.MaxSimultaneousLocksExceeded",
        "Se ha alcanzado el límite máximo de bloqueos simultáneos por usuario.");

    public static Error LockExpired(DateTime expiredAt) => Error.Validation(
        "ReserveSlotLock.LockExpired",
        $"El bloqueo expiró el {expiredAt:yyyy-MM-dd HH:mm:ss}. Inicie el proceso de reserva nuevamente.");
}