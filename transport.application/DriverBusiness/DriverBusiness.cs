using transport.common;
using transport.domain.Drivers;

namespace Transport.Business.DriverBusiness;

public interface IDriverBusiness
{
    Task<Result<int>> Create(DriverCreateRequestDto dto);
}

public class DriverBusiness : IDriverBusiness
{
    public async Task<Result<int>> Create(DriverCreateRequestDto dto)
    {
        if (dto.documentNumber.Contains("37976806"))
        {
            return Result.Failure<int>(DriverError.EmailInBlackList);
        }
        return 1;
    }
}
