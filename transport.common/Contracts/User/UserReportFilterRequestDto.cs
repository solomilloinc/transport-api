using Transport.SharedKernel;

namespace Transport.SharedKernel.Contracts.User;

public sealed record UserReportFilterRequestDto(
    string? Email,
    EntityStatusEnum? Status = null,
    string? Search = null);
