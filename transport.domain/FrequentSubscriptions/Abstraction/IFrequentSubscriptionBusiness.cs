using Transport.SharedKernel;
using Transport.SharedKernel.Contracts.FrequentSubscription;

namespace Transport.Domain.FrequentSubscriptions.Abstraction;

public interface IFrequentSubscriptionBusiness
{
    Task<Result<int>> Create(FrequentSubscriptionCreateRequestDto dto);
    Task<Result<bool>> Update(int frequentSubscriptionId, FrequentSubscriptionUpdateRequestDto dto);
    Task<Result<bool>> Cancel(int frequentSubscriptionId);
    Task<Result<FrequentSubscriptionCancelPreviewDto>> GetCancelPreview(int frequentSubscriptionId);
    Task<Result<FrequentSubscriptionResponseDto>> GetById(int frequentSubscriptionId);
    Task<Result<PagedReportResponseDto<FrequentSubscriptionResponseDto>>> GetReport(
        PagedReportRequestDto<FrequentSubscriptionReportFilterRequestDto> requestDto);
}
