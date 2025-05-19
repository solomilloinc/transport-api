﻿namespace Transport.SharedKernel;

public interface IAuditable
{
    string CreatedBy { get; set; }
    DateTime CreatedDate { get; set; }
    string? UpdatedBy { get; set; }
    DateTime? UpdatedDate { get; set; }
}
