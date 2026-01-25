using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Azure.Functions.Worker;
using Transport.Infraestructure.Authorization;
using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Attributes;
using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Enums;
using System.Net;
using Transport_Api.Extensions;
using Transport_Api.Functions.Base;
using Transport.SharedKernel.Contracts.Trip;
using Transport.Domain.Trips.Abstraction;
using Microsoft.OpenApi.Models;
using Transport.SharedKernel;

namespace Transport_Api.Functions;

public sealed class TripsFunction : FunctionBase
{
    private readonly ITripBusiness _tripBusiness;

    public TripsFunction(ITripBusiness tripBusiness, IServiceProvider serviceProvider)
        : base(serviceProvider)
    {
        _tripBusiness = tripBusiness;
    }

    // Trip CRUD operations

    [Function("CreateTrip")]
    [Authorize("Admin")]
    [OpenApiOperation(operationId: "trip-create", tags: new[] { "Trip" }, Summary = "Create Trip", Description = "Creates a new trip (route)", Visibility = OpenApiVisibilityType.Important)]
    [OpenApiRequestBody("application/json", typeof(TripCreateDto), Required = true)]
    [OpenApiResponseWithBody(HttpStatusCode.OK, "application/json", typeof(int), Summary = "Trip Created")]
    public async Task<HttpResponseData> CreateTrip(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "trip-create")] HttpRequestData req)
    {
        var dto = await req.ReadFromJsonAsync<TripCreateDto>();
        var result = await ValidateAndMatchAsync(req, dto, GetValidator<TripCreateDto>())
                          .BindAsync(_tripBusiness.CreateTrip);

        return await MatchResultAsync(req, result);
    }

    [Function("UpdateTrip")]
    [Authorize("Admin")]
    [OpenApiOperation(operationId: "trip-update", tags: new[] { "Trip" }, Summary = "Update Trip", Description = "Updates an existing trip", Visibility = OpenApiVisibilityType.Important)]
    [OpenApiParameter("tripId", In = ParameterLocation.Path, Required = true, Type = typeof(int), Description = "Trip ID")]
    [OpenApiRequestBody("application/json", typeof(TripCreateDto), Required = true)]
    [OpenApiResponseWithoutBody(HttpStatusCode.OK, Summary = "Trip Updated")]
    public async Task<HttpResponseData> UpdateTrip(
        [HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = "trip-update/{tripId:int}")] HttpRequestData req,
        int tripId)
    {
        var dto = await req.ReadFromJsonAsync<TripCreateDto>();
        var result = await ValidateAndMatchAsync(req, dto, GetValidator<TripCreateDto>())
                          .BindAsync(x => _tripBusiness.UpdateTrip(tripId, x));

        return await MatchResultAsync(req, result);
    }

    [Function("DeleteTrip")]
    [Authorize("Admin")]
    [OpenApiOperation(operationId: "trip-delete", tags: new[] { "Trip" }, Summary = "Delete Trip", Description = "Deletes an existing trip", Visibility = OpenApiVisibilityType.Important)]
    [OpenApiParameter("tripId", In = ParameterLocation.Path, Required = true, Type = typeof(int), Description = "Trip ID")]
    [OpenApiResponseWithoutBody(HttpStatusCode.OK, Summary = "Trip Deleted")]
    public async Task<HttpResponseData> DeleteTrip(
        [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "trip-delete/{tripId:int}")] HttpRequestData req,
        int tripId)
    {
        var result = await _tripBusiness.DeleteTrip(tripId);
        return await MatchResultAsync(req, result);
    }

    [Function("GetTripReport")]
    [Authorize("Admin")]
    [OpenApiOperation(operationId: "trip-report", tags: new[] { "Trip" }, Summary = "Get Trip Report", Description = "Returns paginated list of trips with their prices", Visibility = OpenApiVisibilityType.Important)]
    [OpenApiRequestBody("application/json", typeof(PagedReportRequestDto<TripReportFilterDto>), Required = true)]
    [OpenApiResponseWithBody(HttpStatusCode.OK, "application/json", typeof(PagedReportResponseDto<TripReportResponseDto>), Summary = "Trip Report")]
    public async Task<HttpResponseData> GetTripReport(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "trip-report")] HttpRequestData req)
    {
        var filter = await req.ReadFromJsonAsync<PagedReportRequestDto<TripReportFilterDto>>();
        var result = await _tripBusiness.GetTripReport(filter);
        return await MatchResultAsync(req, result);
    }

    [Function("GetTripById")]
    //[Authorize("Admin", "User")]
    [AllowAnonymous]
    [OpenApiOperation(operationId: "trip-get", tags: new[] { "Trip" }, Summary = "Get Trip by ID", Description = "Returns a trip with its prices. Optionally filter directions by reserveId.", Visibility = OpenApiVisibilityType.Important)]
    [OpenApiParameter("tripId", In = ParameterLocation.Path, Required = true, Type = typeof(int), Description = "Trip ID")]
    [OpenApiParameter("reserveId", In = ParameterLocation.Query, Required = false, Type = typeof(int), Description = "Optional Reserve ID to filter available directions")]
    [OpenApiResponseWithBody(HttpStatusCode.OK, "application/json", typeof(TripReportResponseDto), Summary = "Trip Details")]
    public async Task<HttpResponseData> GetTripById(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "trip/{tripId:int}")] HttpRequestData req,
        int tripId)
    {
        // Parse optional reserveId from query string
        int? reserveId = null;
        var reserveIdParam = req.Query["reserveId"];
        if (!string.IsNullOrEmpty(reserveIdParam) && int.TryParse(reserveIdParam, out var parsedReserveId))
        {
            reserveId = parsedReserveId;
        }

        var result = await _tripBusiness.GetTripById(tripId, reserveId);
        return await MatchResultAsync(req, result);
    }

    // Price management operations

    [Function("AddTripPrice")]
    [Authorize("Admin")]
    [OpenApiOperation(operationId: "trip-price-add", tags: new[] { "Trip" }, Summary = "Add Trip Price", Description = "Adds a price to a trip for a specific city/direction", Visibility = OpenApiVisibilityType.Important)]
    [OpenApiRequestBody("application/json", typeof(TripPriceCreateDto), Required = true)]
    [OpenApiResponseWithBody(HttpStatusCode.OK, "application/json", typeof(int), Summary = "Trip Price Added")]
    public async Task<HttpResponseData> AddTripPrice(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "trip-price-add")] HttpRequestData req)
    {
        var dto = await req.ReadFromJsonAsync<TripPriceCreateDto>();
        var result = await ValidateAndMatchAsync(req, dto, GetValidator<TripPriceCreateDto>())
                          .BindAsync(_tripBusiness.AddPrice);

        return await MatchResultAsync(req, result);
    }

    [Function("UpdateTripPrice")]
    [Authorize("Admin")]
    [OpenApiOperation(operationId: "trip-price-update", tags: new[] { "Trip" }, Summary = "Update Trip Price", Description = "Updates an existing trip price", Visibility = OpenApiVisibilityType.Important)]
    [OpenApiParameter("tripPriceId", In = ParameterLocation.Path, Required = true, Type = typeof(int), Description = "Trip Price ID")]
    [OpenApiRequestBody("application/json", typeof(TripPriceUpdateDto), Required = true)]
    [OpenApiResponseWithoutBody(HttpStatusCode.OK, Summary = "Trip Price Updated")]
    public async Task<HttpResponseData> UpdateTripPrice(
        [HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = "trip-price-update/{tripPriceId:int}")] HttpRequestData req,
        int tripPriceId)
    {
        var dto = await req.ReadFromJsonAsync<TripPriceUpdateDto>();
        var result = await ValidateAndMatchAsync(req, dto, GetValidator<TripPriceUpdateDto>())
                          .BindAsync(x => _tripBusiness.UpdatePrice(tripPriceId, x));

        return await MatchResultAsync(req, result);
    }

    [Function("DeleteTripPrice")]
    [Authorize("Admin")]
    [OpenApiOperation(operationId: "trip-price-delete", tags: new[] { "Trip" }, Summary = "Delete Trip Price", Description = "Deletes an existing trip price", Visibility = OpenApiVisibilityType.Important)]
    [OpenApiParameter("tripPriceId", In = ParameterLocation.Path, Required = true, Type = typeof(int), Description = "Trip Price ID")]
    [OpenApiResponseWithoutBody(HttpStatusCode.OK, Summary = "Trip Price Deleted")]
    public async Task<HttpResponseData> DeleteTripPrice(
        [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "trip-price-delete/{tripPriceId:int}")] HttpRequestData req,
        int tripPriceId)
    {
        var result = await _tripBusiness.DeletePrice(tripPriceId);
        return await MatchResultAsync(req, result);
    }

    [Function("UpdatePricesByPercentage")]
    [Authorize("Admin")]
    [OpenApiOperation(operationId: "trip-prices-update-percentage", tags: new[] { "Trip" }, Summary = "Update Prices by Percentage", Description = "Performs a massive update of trip prices based on a percentage", Visibility = OpenApiVisibilityType.Important)]
    [OpenApiRequestBody("application/json", typeof(PriceMassiveUpdateDto), Required = true)]
    [OpenApiResponseWithoutBody(HttpStatusCode.OK, Summary = "Prices Updated")]
    public async Task<HttpResponseData> UpdatePricesByPercentage(
        [HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = "trip-prices-update-percentage")] HttpRequestData req)
    {
        var dto = await req.ReadFromJsonAsync<PriceMassiveUpdateDto>();
        var result = await ValidateAndMatchAsync(req, dto, GetValidator<PriceMassiveUpdateDto>())
                          .BindAsync(_tripBusiness.UpdatePricesByPercentage);

        return await MatchResultAsync(req, result);
    }
}
