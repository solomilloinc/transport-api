using Transport.SharedKernel;

namespace Transport.SharedKernel.Contracts.User;

public sealed record UserUpdateRequestDto(
    string Email,
    string Role,
    EntityStatusEnum Status);
