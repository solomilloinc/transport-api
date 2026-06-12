using Transport.SharedKernel;

namespace Transport.SharedKernel.Contracts.User;

public sealed record CurrentUserProfileDto(
    int UserId,
    int? CustomerId,
    string Email,
    string Role,
    EntityStatusEnum Status,
    bool NeedsProfileCompletion,
    string? FirstName,
    string? LastName,
    string? DocumentNumber,
    string? Phone1,
    string? Phone2);
