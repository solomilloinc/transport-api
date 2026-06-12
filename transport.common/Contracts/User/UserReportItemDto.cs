using Transport.SharedKernel;

namespace Transport.SharedKernel.Contracts.User;

public sealed record UserReportItemDto(
    int UserId,
    string Email,
    string Role,
    EntityStatusEnum Status,
    int? CustomerId,
    DateTime CreatedDate);
