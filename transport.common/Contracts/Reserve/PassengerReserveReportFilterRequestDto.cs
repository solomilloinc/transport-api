namespace Transport.SharedKernel.Contracts.Reserve;

public record PassengerReserveReportFilterRequestDto(string? PassengerFullName, 
    string? DocumentNumber,
    string? Email);
