using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Azure.Functions.Worker;
using Transport.Infraestructure.Authorization;
using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Attributes;
using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Enums;
using System.Net;
using Transport_Api.Extensions;
using Transport.SharedKernel.Contracts.Customer;
using Transport_Api.Functions.Base;
using Microsoft.OpenApi.Models;
using Transport.Domain.Customers.Abstraction;
using Transport.SharedKernel;

namespace Transport_Api.Functions;

public sealed class CustomerFunction : FunctionBase
{
    private readonly ICustomerBusiness _customerBusiness;

    public CustomerFunction(ICustomerBusiness customerBusiness, IServiceProvider serviceProvider)
        : base(serviceProvider)
    {
        _customerBusiness = customerBusiness;
    }

    [Function("CreateCustomer")]
    [Authorize("Admin")]
    [OpenApiOperation(operationId: "customer-create", tags: new[] { "Customer" }, Summary = "Create new Customer", Description = "Creates a new customer", Visibility = OpenApiVisibilityType.Important)]
    [OpenApiRequestBody("application/json", typeof(CustomerCreateRequestDto), Required = true)]
    [OpenApiResponseWithBody(HttpStatusCode.OK, "application/json", typeof(int), Summary = "Customer Created")]
    public async Task<HttpResponseData> CreateCustomer(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "customer-create")] HttpRequestData req)
    {
        var dto = await req.ReadFromJsonAsync<CustomerCreateRequestDto>();
        var result = await ValidateAndMatchAsync(req, dto, GetValidator<CustomerCreateRequestDto>())
                          .BindAsync(_customerBusiness.Create);

        return await MatchResultAsync(req, result);
    }

    [Function("DeleteCustomer")]
    [Authorize("Admin")]
    [OpenApiOperation(operationId: "customer-delete", tags: new[] { "Customer" }, Summary = "Delete Customer", Description = "Deletes a customer", Visibility = OpenApiVisibilityType.Important)]
    [OpenApiParameter("customerId", In = ParameterLocation.Path, Required = true, Type = typeof(int), Description = "Customer ID")]
    [OpenApiResponseWithoutBody(HttpStatusCode.OK, Summary = "Customer Deleted")]
    public async Task<HttpResponseData> DeleteCustomer(
        [HttpTrigger(AuthorizationLevel.Anonymous, "delete", Route = "customer-delete/{customerId:int}")] HttpRequestData req,
        int customerId)
    {
        var result = await _customerBusiness.Delete(customerId);
        return await MatchResultAsync(req, result);
    }

    [Function("UpdateCustomer")]
    [Authorize("Admin")]
    [OpenApiOperation(operationId: "customer-update", tags: new[] { "Customer" }, Summary = "Update Customer", Description = "Updates an existing customer", Visibility = OpenApiVisibilityType.Important)]
    [OpenApiParameter("customerId", In = ParameterLocation.Path, Required = true, Type = typeof(int), Description = "Customer ID")]
    [OpenApiRequestBody("application/json", typeof(CustomerUpdateRequestDto), Required = true)]
    [OpenApiResponseWithoutBody(HttpStatusCode.OK, Summary = "Customer Updated")]
    public async Task<HttpResponseData> UpdateCustomer(
        [HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = "customer-update/{customerId:int}")] HttpRequestData req,
        int customerId)
    {
        var dto = await req.ReadFromJsonAsync<CustomerUpdateRequestDto>();
        var result = await ValidateAndMatchAsync(req, dto, GetValidator<CustomerUpdateRequestDto>())
                          .BindAsync(x => _customerBusiness.Update(customerId, x));

        return await MatchResultAsync(req, result);
    }

    [Function("GetCustomerReport")]
    [Authorize("Admin")]
    [OpenApiOperation(operationId: "customer-report", tags: new[] { "Customer" }, Summary = "Get Customer Report", Description = "Returns paginated list of customers", Visibility = OpenApiVisibilityType.Important)]
    [OpenApiRequestBody("application/json", typeof(PagedReportRequestDto<CustomerReportFilterRequestDto>), Required = true)]
    [OpenApiResponseWithBody(HttpStatusCode.OK, "application/json", typeof(PagedReportResponseDto<CustomerReportResponseDto>), Summary = "Customer Report")]
    public async Task<HttpResponseData> GetCustomerReport(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "customer-report")] HttpRequestData req)
    {
        var filter = await req.ReadFromJsonAsync<PagedReportRequestDto<CustomerReportFilterRequestDto>>();
        var result = await _customerBusiness.GetCustomerReport(filter);
        return await MatchResultAsync(req, result);
    }
}
