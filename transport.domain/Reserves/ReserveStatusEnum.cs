﻿namespace Transport.Domain.Reserves;

public enum ReserveStatusEnum
{
    Pending = 0,
    Confirmed = 1,
    Cancelled = 2,
    Completed = 3,
    Rejected = 4,
    Expired = 5
}

