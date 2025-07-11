﻿using Transport.SharedKernel;
using Transport.SharedKernel.Contracts.Customer;
using Transport.SharedKernel.Contracts.Reserve;

namespace Transport.Domain.Customers.Abstraction;

public interface ICustomerBusiness
{
    Task<Result<int>> Create(CustomerCreateRequestDto dto);
    Task<Result<bool>> Delete(int customerId);
    Task<Result<PagedReportResponseDto<CustomerReportResponseDto>>> GetCustomerReport(PagedReportRequestDto<CustomerReportFilterRequestDto> requestDto);
    Task<Result<bool>> Update(int customerId, CustomerUpdateRequestDto dto);
    Task<Result<bool>> UpdateStatus(int customerId, EntityStatusEnum status);
    Task<Result<Customer>> GetOrCreateFromPassengerAsync(CustomerReserveCreateRequestDto dto);
    Task<Result<CustomerAccountSummaryDto>> GetCustomerAccountSummaryAsync(int customerId, PagedReportRequestDto<CustomerTransactionReportFilterRequestDto> requestDto);
}
