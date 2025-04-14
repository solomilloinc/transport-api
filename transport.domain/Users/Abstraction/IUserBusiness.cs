using Transport.SharedKernel.Contracts.User;
using Transport.SharedKernel;

namespace Transport.Domain.Users.Abstraction;

public interface ILoginBusiness
{
    Task<Result<LoginResponseDto>> Login(LoginDto login);
}
