namespace Transport.SharedKernel.Contracts.Reserve;

public record SettleCustomerDebtRequestDto(
    int CustomerId,
    List<int> ReserveIds,
    List<CreatePaymentRequestDto> Payments);
