namespace Transport.SharedKernel.Contracts.Reserve;

public record ReserveCreateExternalResultDto(
    bool Success,
    string ExternalReference,
    string PreferenceId
);
