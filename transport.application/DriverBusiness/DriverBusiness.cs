using Microsoft.EntityFrameworkCore;
using transport.common;
using transport.domain.Drivers;
using Transport.Business.Data;
using Transport.Domain.Drivers;

namespace Transport.Business.DriverBusiness;

public interface IDriverBusiness
{
    Task<Result<int>> Create(DriverCreateRequestDto dto);
}

public class DriverBusiness : IDriverBusiness
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IApplicationDbContext _context;

    public DriverBusiness(IUnitOfWork unitOfWork, IApplicationDbContext context)
    {
        _unitOfWork = unitOfWork;
        _context = context;
    }

    public async Task<Result<int>> Create(DriverCreateRequestDto dto)
    {
        Driver driver = await _context.Drivers
            .SingleOrDefaultAsync(x => x.DocumentNumber == dto.documentNumber);

        if (driver != null)
        {
            return Result.Failure<int>(DriverError.DriverAlreadyExist);
        }

        if (dto.documentNumber.Contains("37976806"))
        {
            return Result.Failure<int>(DriverError.EmailInBlackList);
        }

        driver = new Driver
        {
            FirstName = dto.firstName,
            LastName = dto.lastName,
            DocumentNumber = dto.documentNumber
        };

        driver.Raise((new DriverCreatedEvent(driver.DriverId)));
        _context.Drivers.Add(driver);
        await _context.SaveChangesWithOutboxAsync();

        return driver.DriverId;
    }
}
