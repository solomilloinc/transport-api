using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Azure.Functions.Worker;
using Transport.Infraestructure.Authorization;
using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Attributes;
using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Enums;
using System.Net;
using Transport_Api.Extensions;
using Transport_Api.Functions.Base;
using Transport.SharedKernel.Contracts.Service;
using Transport.Domain.Services;
using Transport.Domain.Services.Abstraction;
using Microsoft.OpenApi.Models;
using Transport.SharedKernel;

namespace Transport_Api.Functions;

public sealed class ServicesFunction : FunctionBase
{
    private readonly IServiceBusiness _serviceBusiness;

    public ServicesFunction(IServiceBusiness serviceBusiness, IServiceProvider serviceProvider)
        : base(serviceProvider)
    {
        _serviceBusiness = serviceBusiness;
    }

    [Function("CreateService")]
    [Authorize("Admin")]
    [OpenApiOperation(operationId: "service-create", tags: new[] { "Service" }, Summary = "Create Service", Description = "Creates a new service", Visibility = OpenApiVisibilityType.Important)]
    [OpenApiRequestBody("application/json", typeof(ServiceCreateRequestDto), Required = true)]
    [OpenApiResponseWithBody(HttpStatusCode.OK, "application/json", typeof(int), Summary = "Service Created")]
    public async Task<HttpResponseData> CreateService(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "service-create")] HttpRequestData req)
    {
        var dto = await req.ReadFromJsonAsync<ServiceCreateRequestDto>();
        var result = await ValidateAndMatchAsync(req, dto, GetValidator<ServiceCreateRequestDto>())
                          .BindAsync(_serviceBusiness.Create);

        return await MatchResultAsync(req, result);
    }

    [Function("DeleteService")]
    [Authorize("Admin")]
    [OpenApiOperation(operationId: "service-delete", tags: new[] { "Service" }, Summary = "Delete Service", Description = "Deletes an existing service", Visibility = OpenApiVisibilityType.Important)]
    [OpenApiParameter("serviceId", In = ParameterLocation.Path, Required = true, Type = typeof(int), Description = "Service ID")]
    [OpenApiResponseWithoutBody(HttpStatusCode.OK, Summary = "Service Deleted")]
    public async Task<HttpResponseData> DeleteService(
        [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "service-delete/{serviceId:int}")] HttpRequestData req,
        int serviceId)
    {
        var result = await _serviceBusiness.Delete(serviceId);
        return await MatchResultAsync(req, result);
    }

    [Function("UpdateService")]
    [Authorize("Admin")]
    [OpenApiOperation(operationId: "service-update", tags: new[] { "Service" }, Summary = "Update Service", Description = "Updates an existing service", Visibility = OpenApiVisibilityType.Important)]
    [OpenApiParameter("serviceId", In = ParameterLocation.Path, Required = true, Type = typeof(int), Description = "Service ID")]
    [OpenApiRequestBody("application/json", typeof(ServiceUpdateRequestDto), Required = true)]
    [OpenApiResponseWithoutBody(HttpStatusCode.OK, Summary = "Service Updated")]
    public async Task<HttpResponseData> UpdateService(
        [HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = "service-update/{serviceId:int}")] HttpRequestData req,
        int serviceId)
    {
        // Se lee el DTO de Update (antes leía ServiceCreateRequestDto, lo cual era un
        // desalineo con el atributo OpenApiRequestBody declarado arriba y forzaba al
        // cliente a enviar campos que no tenían sentido en un update, como TripId y
        // Schedules. Ver ServiceUpdateRequestDto para el contrato actual.
        var dto = await req.ReadFromJsonAsync<ServiceUpdateRequestDto>();
        var result = await ValidateAndMatchAsync(req, dto, GetValidator<ServiceUpdateRequestDto>())
                          .BindAsync(x => _serviceBusiness.Update(serviceId, x));

        return await MatchResultAsync(req, result);
    }

    [Function("GetServiceReport")]
    [Authorize("Admin")]
    [OpenApiOperation(operationId: "service-report", tags: new[] { "Service" }, Summary = "Get Service Report", Description = "Returns paginated list of services", Visibility = OpenApiVisibilityType.Important)]
    [OpenApiRequestBody("application/json", typeof(PagedReportRequestDto<ServiceReportFilterRequestDto>), Required = true)]
    [OpenApiResponseWithBody(HttpStatusCode.OK, "application/json", typeof(PagedReportResponseDto<ServiceReportResponseDto>), Summary = "Service Report")]
    public async Task<HttpResponseData> GetServiceReport(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "service-report")] HttpRequestData req)
    {
        var filter = await req.ReadFromJsonAsync<PagedReportRequestDto<ServiceReportFilterRequestDto>>();
        var result = await _serviceBusiness.GetServiceReport(filter);
        return await MatchResultAsync(req, result);
    }

    [Function("GetActiveServicesList")]
    [Authorize("Admin")]
    [OpenApiOperation(operationId: "services-list", tags: new[] { "Service" }, Summary = "Get Active Services List", Description = "Returns a list of active services (Id and Name) for dropdowns", Visibility = OpenApiVisibilityType.Important)]
    [OpenApiResponseWithBody(HttpStatusCode.OK, "application/json", typeof(List<ServiceIdNameDto>), Summary = "Active Services List")]
    public async Task<HttpResponseData> GetActiveServicesList(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "services-list")] HttpRequestData req)
    {
        var result = await _serviceBusiness.GetActiveServicesListAsync();
        return await MatchResultAsync(req, result);
    }

    // ─────────────────────────────────────────────────────────────────────────
    //  ServiceSchedule endpoints
    //
    //  Los schedules se manejan siempre "por servicio": para crearlos o listarlos
    //  el cliente elige primero el Service (serviceId va por ruta) y el resto de
    //  operaciones (update/delete/status) apuntan directo al scheduleId, que ya
    //  pertenece inequívocamente a un servicio a través de su FK.
    //
    //  Por qué son endpoints aparte y no parte del payload de Service Update:
    //  mezclarlos obligaba a "soft-delete + recrear" toda la lista de schedules
    //  en cada edición del servicio, invalidando reservas ya ligadas. Separarlos
    //  permite el ciclo de vida granular (agregar un horario nuevo, desactivar
    //  uno viejo) sin tocar el resto.
    // ─────────────────────────────────────────────────────────────────────────

    [Function("GetServiceSchedules")]
    [Authorize("Admin")]
    [OpenApiOperation(operationId: "service-schedules-list", tags: new[] { "ServiceSchedule" }, Summary = "Get Schedules By Service", Description = "Returns all schedules (horarios de salida) asociados a un servicio", Visibility = OpenApiVisibilityType.Important)]
    [OpenApiParameter("serviceId", In = ParameterLocation.Path, Required = true, Type = typeof(int), Description = "Service ID")]
    [OpenApiResponseWithBody(HttpStatusCode.OK, "application/json", typeof(List<ServiceSchedule>), Summary = "Service Schedules")]
    public async Task<HttpResponseData> GetServiceSchedules(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "service-schedules/{serviceId:int}")] HttpRequestData req,
        int serviceId)
    {
        var result = await _serviceBusiness.GetSchedulesByServiceId(serviceId);
        return await MatchResultAsync(req, result);
    }

    [Function("CreateServiceSchedule")]
    [Authorize("Admin")]
    [OpenApiOperation(operationId: "service-schedule-create", tags: new[] { "ServiceSchedule" }, Summary = "Create Service Schedule", Description = "Crea un nuevo horario de salida para el servicio indicado en la ruta. El ServiceId del body se ignora; prevalece el de la ruta.", Visibility = OpenApiVisibilityType.Important)]
    [OpenApiParameter("serviceId", In = ParameterLocation.Path, Required = true, Type = typeof(int), Description = "Service ID")]
    [OpenApiRequestBody("application/json", typeof(ServiceScheduleCreateDto), Required = true)]
    [OpenApiResponseWithBody(HttpStatusCode.OK, "application/json", typeof(int), Summary = "Schedule Created")]
    public async Task<HttpResponseData> CreateServiceSchedule(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "service-schedule-create/{serviceId:int}")] HttpRequestData req,
        int serviceId)
    {
        var dto = await req.ReadFromJsonAsync<ServiceScheduleCreateDto>();
        var result = await ValidateAndMatchAsync(req, dto, GetValidator<ServiceScheduleCreateDto>())
                          .BindAsync(x => _serviceBusiness.CreateSchedule(serviceId, x));

        return await MatchResultAsync(req, result);
    }

    [Function("UpdateServiceSchedule")]
    [Authorize("Admin")]
    [OpenApiOperation(operationId: "service-schedule-update", tags: new[] { "ServiceSchedule" }, Summary = "Update Service Schedule", Description = "Actualiza la hora de salida y flag de feriado de un schedule existente", Visibility = OpenApiVisibilityType.Important)]
    [OpenApiParameter("scheduleId", In = ParameterLocation.Path, Required = true, Type = typeof(int), Description = "Service Schedule ID")]
    [OpenApiRequestBody("application/json", typeof(ServiceScheduleUpdateDto), Required = true)]
    [OpenApiResponseWithoutBody(HttpStatusCode.OK, Summary = "Schedule Updated")]
    public async Task<HttpResponseData> UpdateServiceSchedule(
        [HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = "service-schedule-update/{scheduleId:int}")] HttpRequestData req,
        int scheduleId)
    {
        var dto = await req.ReadFromJsonAsync<ServiceScheduleUpdateDto>();
        var result = await ValidateAndMatchAsync(req, dto, GetValidator<ServiceScheduleUpdateDto>())
                          .BindAsync(x => _serviceBusiness.UpdateSchedule(scheduleId, x));

        return await MatchResultAsync(req, result);
    }

    [Function("DeleteServiceSchedule")]
    [Authorize("Admin")]
    [OpenApiOperation(operationId: "service-schedule-delete", tags: new[] { "ServiceSchedule" }, Summary = "Delete Service Schedule", Description = "Soft-delete de un schedule (marca Status = Deleted, no borra la fila para preservar la integridad de las reservas históricas)", Visibility = OpenApiVisibilityType.Important)]
    [OpenApiParameter("scheduleId", In = ParameterLocation.Path, Required = true, Type = typeof(int), Description = "Service Schedule ID")]
    [OpenApiResponseWithoutBody(HttpStatusCode.OK, Summary = "Schedule Deleted")]
    public async Task<HttpResponseData> DeleteServiceSchedule(
        [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "service-schedule-delete/{scheduleId:int}")] HttpRequestData req,
        int scheduleId)
    {
        var result = await _serviceBusiness.DeleteSchedule(scheduleId);
        return await MatchResultAsync(req, result);
    }

    [Function("SyncServiceSchedules")]
    [Authorize("Admin")]
    [OpenApiOperation(
        operationId: "service-schedules-sync",
        tags: new[] { "ServiceSchedule" },
        Summary = "Sync Service Schedules (bulk)",
        Description = "Sincroniza la lista completa de schedules de un servicio en una sola operación atómica. " +
                      "Ver ServiceSchedulesSyncRequestDto para la semántica declarativa: items con ServiceScheduleId=null se crean, " +
                      "items con Id existente se actualizan (y se reactivan si estaban borrados), " +
                      "schedules en DB que no aparecen en el payload se soft-deletean. " +
                      "Todo dentro de una única transacción — o pasa todo o nada.",
        Visibility = OpenApiVisibilityType.Important)]
    [OpenApiParameter("serviceId", In = ParameterLocation.Path, Required = true, Type = typeof(int), Description = "Service ID")]
    [OpenApiRequestBody("application/json", typeof(ServiceSchedulesSyncRequestDto), Required = true)]
    [OpenApiResponseWithoutBody(HttpStatusCode.OK, Summary = "Schedules Synced")]
    public async Task<HttpResponseData> SyncServiceSchedules(
        [HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = "service-schedules-sync/{serviceId:int}")] HttpRequestData req,
        int serviceId)
    {
        var dto = await req.ReadFromJsonAsync<ServiceSchedulesSyncRequestDto>();
        var result = await ValidateAndMatchAsync(req, dto, GetValidator<ServiceSchedulesSyncRequestDto>())
                          .BindAsync(x => _serviceBusiness.SyncSchedules(serviceId, x));

        return await MatchResultAsync(req, result);
    }

    [Function("UpdateServiceScheduleStatus")]
    [Authorize("Admin")]
    [OpenApiOperation(operationId: "service-schedule-updatestatus", tags: new[] { "ServiceSchedule" }, Summary = "Update Service Schedule Status", Description = "Cambia el estado (Active/Inactive/Deleted) de un schedule. Útil para pausar un horario sin borrarlo definitivamente.", Visibility = OpenApiVisibilityType.Important)]
    [OpenApiParameter("scheduleId", In = ParameterLocation.Path, Required = true, Type = typeof(int), Description = "Service Schedule ID")]
    [OpenApiParameter("status", In = ParameterLocation.Query, Required = true, Type = typeof(EntityStatusEnum), Description = "New status")]
    [OpenApiResponseWithoutBody(HttpStatusCode.OK, Summary = "Status Updated")]
    public async Task<HttpResponseData> UpdateServiceScheduleStatus(
        [HttpTrigger(AuthorizationLevel.Anonymous, "patch", Route = "service-schedule-status/{scheduleId:int}")] HttpRequestData req,
        int scheduleId)
    {
        var queryParams = System.Web.HttpUtility.ParseQueryString(req.Url.Query);
        var statusParsed = Enum.TryParse<EntityStatusEnum>(queryParams["status"], true, out var status);

        if (!statusParsed)
            throw new ArgumentException("Invalid status value");

        var result = await _serviceBusiness.UpdateScheduleStatus(scheduleId, status);
        return await MatchResultAsync(req, result);
    }
}
