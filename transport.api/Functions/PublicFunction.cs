using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Attributes;
using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Enums;
using System.Net;
using Transport_Api.Extensions;
using Transport_Api.Functions.Base;
using Transport.SharedKernel.Contracts.Trip;
using Transport.Domain.Trips.Abstraction;
using Microsoft.OpenApi.Models;
using Transport.SharedKernel;
using Transport.Infraestructure.Authorization;

namespace Transport_Api.Functions;

/// <summary>
/// Public endpoints that don't require authentication.
/// Used for landing page and public-facing features.
/// </summary>
public sealed class PublicFunction : FunctionBase
{
    private readonly ITripBusiness _tripBusiness;

    public PublicFunction(ITripBusiness tripBusiness, IServiceProvider serviceProvider)
        : base(serviceProvider)
    {
        _tripBusiness = tripBusiness;
    }

    [Function("GetPublicTrips")]
    [AllowAnonymous]
    [OpenApiOperation(operationId: "public-trips", tags: new[] { "Public" }, Summary = "Get Public Trips", Description = "Returns active trips for landing page display. No authentication required.", Visibility = OpenApiVisibilityType.Important)]
    [OpenApiParameter("pageNumber", In = ParameterLocation.Query, Required = false, Type = typeof(int), Description = "Page number (default: 1)")]
    [OpenApiParameter("pageSize", In = ParameterLocation.Query, Required = false, Type = typeof(int), Description = "Page size (default: 100)")]
    [OpenApiResponseWithBody(HttpStatusCode.OK, "application/json", typeof(PagedReportResponseDto<PublicTripDto>), Summary = "Public Trips List")]
    public async Task<HttpResponseData> GetPublicTrips(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "public/trips")] HttpRequestData req)
    {
        // Parse optional query parameters
        int pageNumber = 1;
        int pageSize = 100;

        var pageNumberParam = req.Query["pageNumber"];
        if (!string.IsNullOrEmpty(pageNumberParam) && int.TryParse(pageNumberParam, out var parsedPageNumber))
        {
            pageNumber = parsedPageNumber;
        }

        var pageSizeParam = req.Query["pageSize"];
        if (!string.IsNullOrEmpty(pageSizeParam) && int.TryParse(pageSizeParam, out var parsedPageSize))
        {
            pageSize = parsedPageSize;  
        }

        var result = await _tripBusiness.GetPublicTrips(pageNumber, pageSize);
        return await MatchResultAsync(req, result);
    }
}
