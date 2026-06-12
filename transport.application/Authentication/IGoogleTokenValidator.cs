using Transport.SharedKernel.Contracts.User;

namespace Transport.Business.Authentication;

public interface IGoogleTokenValidator
{
    Task<GoogleAuthenticatedUserDto> ValidateAsync(string idToken, CancellationToken cancellationToken = default);
}
