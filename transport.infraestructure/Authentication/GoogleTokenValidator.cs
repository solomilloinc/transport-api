using Google.Apis.Auth;
using Microsoft.Extensions.Configuration;
using Transport.Business.Authentication;
using Transport.Domain.Users;
using Transport.SharedKernel.Contracts.User;

namespace Transport.Infraestructure.Authentication;

internal sealed class GoogleTokenValidator : IGoogleTokenValidator
{
    private readonly string clientId;

    public GoogleTokenValidator(IConfiguration configuration)
    {
        clientId = configuration["Google:ClientId"]
            ?? configuration["Authentication:Google:ClientId"]
            ?? throw new InvalidOperationException("Google ClientId is not configured.");
    }

    public async Task<GoogleAuthenticatedUserDto> ValidateAsync(string idToken, CancellationToken cancellationToken = default)
    {
        var payload = await GoogleJsonWebSignature.ValidateAsync(
            idToken,
            new GoogleJsonWebSignature.ValidationSettings
            {
                Audience = new[] { clientId }
            });

        if (!payload.EmailVerified)
        {
            throw new InvalidOperationException(UserError.GoogleEmailNotVerified.Description);
        }

        return new GoogleAuthenticatedUserDto(
            payload.Email,
            payload.GivenName ?? string.Empty,
            payload.FamilyName ?? string.Empty,
            payload.Subject,
            payload.EmailVerified,
            payload.Picture,
            null);
    }
}
