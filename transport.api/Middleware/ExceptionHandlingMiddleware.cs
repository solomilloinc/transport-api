using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Middleware;
using Microsoft.Extensions.Logging;
using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.IdentityModel.Tokens;
using transport.common;
using Microsoft.AspNetCore.Http;

namespace transport_api.Middleware;

public class ExceptionHandlingMiddleware : IFunctionsWorkerMiddleware
{
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    public ExceptionHandlingMiddleware(ILogger<ExceptionHandlingMiddleware> logger)
    {
        _logger = logger;
    }

    public async Task Invoke(FunctionContext context, FunctionExecutionDelegate next)
    {
        try
        {
            await next(context);

            var result = context.GetInvocationResult().Value;

            if (result is Result { IsSuccess: false } failure)
            {
                await SetProblemDetailsResponse(context, failure);
            }
        }
        catch (Exception ex) when (IsUnauthorized(ex))
        {
            _logger.LogWarning(ex, "Unauthorized access");

            await SetProblemDetailsResponse(context, new ProblemDetails
            {
                Status = StatusCodes.Status401Unauthorized,
                Title = "Unauthorized",
                Detail = ex.Message,
                Type = "https://tools.ietf.org/html/rfc7235#section-3.1"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unhandled exception");

            await SetProblemDetailsResponse(context, new ProblemDetails
            {
                Status = StatusCodes.Status500InternalServerError,
                Title = "Server failure",
                Detail = ex.Message,
                Type = "https://tools.ietf.org/html/rfc7231#section-6.6.1"
            });
        }
    }

    private bool IsUnauthorized(Exception ex) =>
        ex is UnauthorizedAccessException or SecurityTokenException;

    private async Task SetProblemDetailsResponse(FunctionContext context, ProblemDetails details)
    {
        var httpRequest = await context.GetHttpRequestDataAsync();
        var response = httpRequest.CreateResponse((HttpStatusCode)details.Status!);
        response.Headers.Add("Content-Type", "application/json");
        await response.WriteStringAsync(JsonSerializer.Serialize(details));
        context.GetInvocationResult().Value = response;
    }

    private async Task SetProblemDetailsResponse(FunctionContext context, Result result)
    {
        var httpRequest = await context.GetHttpRequestDataAsync();
        var response = httpRequest.CreateResponse();
        var problem = Infrastructure.CustomResults.ToProblemDetails(result);
        response.StatusCode = (HttpStatusCode)problem.Status!;
        response.Headers.Add("Content-Type", "application/json");
        await response.WriteStringAsync(JsonSerializer.Serialize(problem));
        context.GetInvocationResult().Value = response;
    }
}
