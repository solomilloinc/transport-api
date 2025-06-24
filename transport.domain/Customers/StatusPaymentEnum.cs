namespace Transport.Domain.Customers;

public enum StatusPaymentEnum
{
    Pending = 1, //Espero confirmaciòn de webhook
    Paid = 2, //Ida (online siempre es pagado)
    Cancelled = 3,
    Refunded = 4,
    PrePayment = 5 //se setea cuando es un pago de ida y vuelta
}
