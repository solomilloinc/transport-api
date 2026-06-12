using Transport.SharedKernel.Contracts.User;
using Transport.SharedKernel;

namespace Transport.Domain.Users.Abstraction;

public interface IUserBusiness
{
    Task<Result<LoginResponseDto>> Login(LoginDto login);
    Task<Result<LoginResponseDto>> RegisterClientAsync(ClientRegisterRequestDto request);
    Task<Result<LoginResponseDto>> LoginWithGoogleAsync(GoogleAuthenticatedUserDto googleUser);
    Task<Result<RefreshTokenResponseDto>> RenewTokenAsync(string token, string ipAddress);
    Task LogoutAsync(string refreshToken, string ipAddress);
    Task RevokeAllSessionsAsync(int userId, string ipAddress);
    Task<Result<CurrentUserProfileDto>> GetCurrentProfileAsync(int userId);
    Task<Result<CurrentUserProfileDto>> CompleteClientProfileAsync(int userId, ClientProfileCompleteRequestDto request);
    Task<Result<int>> CreateOperativeAsync(UserCreateRequestDto request);
    Task<Result<bool>> UpdateOperativeAsync(int userId, UserUpdateRequestDto request);
    Task<Result<PagedReportResponseDto<UserReportItemDto>>> GetOperativeUsersReportAsync(PagedReportRequestDto<UserReportFilterRequestDto> request);
}
