using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Transport.Domain.Customers;

public enum CustomerReserveStatusEnum
{
    PendingPayment = 1,
    Confirmed = 2,
    Cancelled = 3,
}
