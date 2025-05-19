using Microsoft.EntityFrameworkCore;
using Transport.SharedKernel;
using Transport.Business.Data;
using Transport.Domain.Customers;
using Transport.Domain.Customers.Abstraction;
using Transport.SharedKernel.Contracts.Customer;
using System.Linq.Expressions;

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
            return Result.Failure<int>(CustomerError.CustomerAlreadyExist);
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

        return customer.CustomerId;
    }

    public async Task<Result<bool>> Delete(int customerId)
    {
        var customer = await _context.Customers.FindAsync(customerId);

        if (customer is null)
        {
            return Result.Failure<bool>(CustomerError.CustomerNotFound);
        }

        customer.Status = EntityStatusEnum.Deleted;
        _context.Customers.Update(customer);

        await _context.SaveChangesWithOutboxAsync();
        return Result.Success(true);
    }

    public async Task<Result<PagedReportResponseDto<CustomerReportResponseDto>>> GetCustomerReport(PagedReportRequestDto<CustomerReportFilterRequestDto> requestDto)
    {
        var query = _context.Customers
            .AsNoTracking()
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(requestDto.Filters?.FirstName))
            query = query.Where(x => x.FirstName.Contains(requestDto.Filters.FirstName));

        if (!string.IsNullOrWhiteSpace(requestDto.Filters?.LastName))
            query = query.Where(x => x.LastName.Contains(requestDto.Filters.LastName));

        if (!string.IsNullOrWhiteSpace(requestDto.Filters?.DocumentNumber))
            query = query.Where(x => x.DocumentNumber.Contains(requestDto.Filters.DocumentNumber));

        var sortMappings = new Dictionary<string, Expression<Func<Customer, object>>>
        {
            ["firstname"] = c => c.FirstName,
            ["lastname"] = c => c.LastName,
            ["documentnumber"] = c => c.DocumentNumber,
            ["email"] = c => c.Email
        };

        var pagedResult = await query.ToPagedReportAsync<CustomerReportResponseDto, Customer, CustomerReportFilterRequestDto>(
            requestDto,
            selector: c => new CustomerReportResponseDto(c.CustomerId, c.FirstName, c.LastName, c.Email, c.DocumentNumber, c.Phone1, c.Phone2, c.CreatedDate),
            sortMappings: sortMappings
        );

        return Result.Success(pagedResult);
    }

    public async Task<Result<bool>> Update(int customerId, CustomerUpdateRequestDto dto)
    {
        var customer = await _context.Customers.FindAsync(customerId);

        if (customer is null)
        {
            return Result.Failure<bool>(CustomerError.CustomerNotFound);
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
        var customer = await _context.Customers.FindAsync(customerId);

        if (customer is null)
        {
            return Result.Failure<bool>(CustomerError.CustomerNotFound);
        }

        customer.Status = status;
        await _context.SaveChangesWithOutboxAsync();

        return Result.Success(true);
    }
}
