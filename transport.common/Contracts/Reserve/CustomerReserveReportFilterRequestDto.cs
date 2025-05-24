namespace Transport.SharedKernel.Contracts.Reserve;

public record CustomerReserveReportFilterRequestDto(string CustomerFullName, 
    string DocumentNumber,
    string Email);
