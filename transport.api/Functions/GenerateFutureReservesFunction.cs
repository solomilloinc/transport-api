using System.Net;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Extensions.Logging;
using Transport.Domain.Services.Abstraction;

namespace transport_api.Functions;

public class GenerateFutureReservesFunction
{
    private readonly ILogger<GenerateFutureReservesFunction> _logger;
    private readonly IServiceBusiness _serviceBusiness;

    public GenerateFutureReservesFunction(ILogger<GenerateFutureReservesFunction> logger, IServiceBusiness serviceBusiness)
    {
        _logger = logger;
        _serviceBusiness = serviceBusiness;
    }

    [Function("GenerateFutureReservesFunction")]
    public async Task<HttpResponseData> Run(
     [HttpTrigger(AuthorizationLevel.Anonymous, "get", "post")] HttpRequestData req)
    {
        _logger.LogInformation("C# HTTP trigger function processed a request.");

        var result = await _serviceBusiness.GenerateFutureReservesAsync();

        var response = req.CreateResponse(HttpStatusCode.OK);
        await response.WriteStringAsync("Welcome to Azure Functions!");

        return response;
    }
}