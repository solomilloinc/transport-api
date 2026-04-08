using Microsoft.EntityFrameworkCore;
using Transport.SharedKernel;
using Transport.Business.Data;
using Transport.Domain.Customers;
using Transport.Domain.Customers.Abstraction;
using Transport.SharedKernel.Contracts.Customer;
using System.Linq.Expressions;
using Transport.Domain.Reserves;
using Transport.SharedKernel.Contracts.Reserve;
using Transport.Domain.Services;

namespace Transport.Business.CustomerBusiness;

public class CustomerBusiness : ICustomerBusiness
{
    private readonly IApplicationDbContext _context;

    public CustomerBusiness(IApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<Result<int>> Create(CustomerCreateRequestDto dto)
    {
        var customer = await _context.Customers
            .SingleOrDefaultAsync(x => x.DocumentNumber == dto.DocumentNumber);

        if (customer != null)
        {
            return Result.Failure<int>(CustomerError.AlreadyExists);
        }

        if (dto.ServiceIds?.Any() == true)
        {
            var validationResult = await ValidateServiceIdsAsync(dto.ServiceIds);
            if (validationResult.IsFailure)
                return Result.Failure<int>(validationResult.Error);
        }

        customer = new Customer
        {
            FirstName = dto.FirstName,
            LastName = dto.LastName,
            DocumentNumber = dto.DocumentNumber,
            Email = dto.Email,
            Phone1 = dto.Phone1,
            Phone2 = dto.Phone2
        };

        _context.Customers.Add(customer);
        await _context.SaveChangesWithOutboxAsync();

        if (dto.ServiceIds?.Any() == true)
        {
            foreach (var serviceId in dto.ServiceIds.Distinct())
            {
                _context.ServiceCustomers.Add(new ServiceCustomer
                {
                    CustomerId = customer.CustomerId,
                    ServiceId = serviceId
                });
            }
            await _context.SaveChangesWithOutboxAsync();
        }

        return customer.CustomerId;
    }

    public async Task<Result<bool>> Delete(int customerId)
    {
        var customer = await _context.Customers.Where(x => x.CustomerId == customerId).FirstOrDefaultAsync();

        if (customer is null)
        {
            return Result.Failure<bool>(CustomerError.NotFound);
        }

        customer.Status = EntityStatusEnum.Deleted;
        _context.Customers.Update(customer);

        await _context.SaveChangesWithOutboxAsync();
        return Result.Success(true);
    }

    public async Task<Result<PagedReportResponseDto<CustomerReportResponseDto>>> GetCustomerReport(PagedReportRequestDto<CustomerReportFilterRequestDto> requestDto)
    {
        var query = _context.Customers
            .Include(c => c.Services)
                .ThenInclude(sc => sc.Service)
            .AsNoTracking()
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(requestDto.Filters?.FirstName))
            query = query.Where(x => x.FirstName.Contains(requestDto.Filters.FirstName));

        if (!string.IsNullOrWhiteSpace(requestDto.Filters?.LastName))
            query = query.Where(x => x.LastName.Contains(requestDto.Filters.LastName));

        if (!string.IsNullOrWhiteSpace(requestDto.Filters?.DocumentNumber))
            query = query.Where(x => x.DocumentNumber.Contains(requestDto.Filters.DocumentNumber));

        if (requestDto.Filters?.Status is not null)
            query = query.Where(x => x.Status == requestDto.Filters.Status);
        else
            query = query.Where(x => x.Status == EntityStatusEnum.Active);

        var sortMappings = new Dictionary<string, Expression<Func<Customer, object>>>
        {
            ["firstname"] = c => c.FirstName,
            ["lastname"] = c => c.LastName,
            ["documentnumber"] = c => c.DocumentNumber,
            ["email"] = c => c.Email
        };

        var pagedResult = await query.ToPagedReportAsync<CustomerReportResponseDto, Customer, CustomerReportFilterRequestDto>(
            requestDto,
            selector: c => new CustomerReportResponseDto(c.CustomerId, c.FirstName, c.LastName, c.Email, c.DocumentNumber, c.Phone1, c.Phone2, c.CreatedDate, c.CurrentBalance, c.Services.Select(s => new CustomerServiceDto(s.ServiceId, s.Service.Name)).ToList()),
            sortMappings: sortMappings
        );

        return Result.Success(pagedResult);
    }

    public async Task<Result<bool>> Update(int customerId, CustomerUpdateRequestDto dto)
    {
        var customer = await _context.Customers.Where(x => x.CustomerId == customerId).FirstOrDefaultAsync();

        if (customer is null)
        {
            return Result.Failure<bool>(CustomerError.NotFound);
        }

        if (dto.ServiceIds is not null)
        {
            if (dto.ServiceIds.Any())
            {
                var validationResult = await ValidateServiceIdsAsync(dto.ServiceIds);
                if (validationResult.IsFailure)
                    return Result.Failure<bool>(validationResult.Error);
            }

            var existingRelations = await _context.ServiceCustomers
                .Where(sc => sc.CustomerId == customerId)
                .ToListAsync();
            _context.ServiceCustomers.RemoveRange(existingRelations);

            foreach (var serviceId in dto.ServiceIds.Distinct())
            {
                _context.ServiceCustomers.Add(new ServiceCustomer
                {
                    CustomerId = customerId,
                    ServiceId = serviceId
                });
            }
        }

        customer.FirstName = dto.FirstName;
        customer.LastName = dto.LastName;
        customer.Email = dto.Email;
        customer.Phone1 = dto.Phone1;
        customer.Phone2 = dto.Phone2;

        _context.Customers.Update(customer);
        await _context.SaveChangesWithOutboxAsync();

        return Result.Success(true);
    }

    public async Task<Result<bool>> UpdateStatus(int customerId, EntityStatusEnum status)
    {
        var customer = await _context.Customers.Where(x => x.CustomerId == customerId).FirstOrDefaultAsync();

        if (customer is null)
        {
            return Result.Failure<bool>(CustomerError.NotFound);
        }

        customer.Status = status;
        await _context.SaveChangesWithOutboxAsync();

        return Result.Success(true);
    }   


    public async Task<Result<CustomerAccountSummaryDto>> GetCustomerAccountSummaryAsync(int customerId, PagedReportRequestDto<CustomerTransactionReportFilterRequestDto> requestDto)
    {
        var customer = await _context.Customers
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.CustomerId == customerId);

        if (customer is null)
            return Result.Failure<CustomerAccountSummaryDto>(CustomerError.NotFound);

        var query = _context.CustomerAccountTransactions
            .AsNoTracking()
            .Where(t => t.CustomerId == customerId);

        if (requestDto.Filters.FromDate.HasValue)
            query = query.Where(t => t.Date >= requestDto.Filters.FromDate.Value);

        if (requestDto.Filters.ToDate.HasValue)
            query = query.Where(t => t.Date <= requestDto.Filters.ToDate.Value);

        if (requestDto.Filters.TransactionType != null)
        {
            query = query.Where(t => t.Type == (TransactionType)requestDto.Filters.TransactionType);
        }

        var sortMappings = new Dictionary<string, Expression<Func<CustomerAccountTransaction, object>>>
        {
            ["date"] = t => t.Date,
            ["description"] = t => t.Description,
            ["amount"] = t => t.Amount,
            ["transactiontype"] = t => t.Type
        };

        // Orden por defecto: fecha descendente (más recientes primero)
        if (string.IsNullOrWhiteSpace(requestDto.SortBy))
        {
            requestDto.SortBy = "date";
            requestDto.SortDescending = true;
        }

        var pagedResult = await query.ToPagedReportAsync<CustomerTransactionDto, CustomerAccountTransaction, CustomerTransactionReportFilterRequestDto>(
            requestDto,
            selector: t => new CustomerTransactionDto(t.CustomerAccountTransactionId, t.CustomerId, t.Description, t.Type.ToString(), t.Amount, t.Date),
            sortMappings: sortMappings
        );

        var result = new CustomerAccountSummaryDto(
            CustomerId: customer.CustomerId,
            CustomerFullName: $"{customer.FirstName} {customer.LastName}",
            CurrentBalance: customer.CurrentBalance,
            Transactions: pagedResult
        );

        return Result.Success(result);
    }

    private async Task<Result<bool>> ValidateServiceIdsAsync(List<int> serviceIds)
    {
        var distinctIds = serviceIds.Distinct().ToList();
        var activeServiceIds = await _context.Services
            .Where(s => distinctIds.Contains(s.ServiceId) && s.Status == EntityStatusEnum.Active)
            .Select(s => s.ServiceId)
            .ToListAsync();

        var invalidServiceId = distinctIds.FirstOrDefault(id => !activeServiceIds.Contains(id));
        if (invalidServiceId != 0)
        {
            return Result.Failure<bool>(ServiceError.ServiceNotActive(invalidServiceId));
        }

        return Result.Success(true);
    }
}
