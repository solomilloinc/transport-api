using Transport.SharedKernel;

namespace Transport.Domain.CashBoxes;

public static class CashBoxError
{
    public static readonly Error NotFound = Error.NotFound(
        "CashBox.NotFound",
        "No se encontro la caja especificada.");

    public static readonly Error AlreadyClosed = Error.Conflict(
        "CashBox.AlreadyClosed",
        "La caja ya esta cerrada.");

    public static readonly Error NoOpenCashBox = Error.Validation(
        "CashBox.NoOpenCashBox",
        "No hay ninguna caja abierta. Debe abrir una caja antes de registrar pagos.");

    public static readonly Error CannotCloseWithPendingPayments = Error.Validation(
        "CashBox.CannotCloseWithPendingPayments",
        "No se puede cerrar la caja con pagos pendientes.");
}
