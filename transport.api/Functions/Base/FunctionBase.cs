using Microsoft.Azure.Functions.Worker.Http;
using System.Net;
using Transport.SharedKernel;
using Transport_Api.Infrastructure;
using Transport_Api.Extensions;
using Microsoft.AspNetCore.Http;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;

namespace Transport_Api.Functions.Base;

public abstract class FunctionBase
{
    protected readonly IServiceProvider _serviceProvider;

    protected FunctionBase(IServiceProvider serviceProvider)
    {
        _serviceProvider = serviceProvider;
    }

    protected IValidator<T> GetValidator<T>()
    {
        var validator = _serviceProvider.GetService<IValidator<T>>();
        if (validator is null)
            throw new InvalidOperationException($"No validator registered for type {typeof(T).Name}");
        return validator;
    }

    protected async Task<Result<T>> ValidateAndMatchAsync<T>(
      HttpRequestData req,
      T dto,
      IValidator<T> validator)
    {
        var validationResult = await validator.ValidateAsync(dto);

        if (validationResult.IsValid)
            return Result.Success(dto);

        var errors = validationResult.Errors
                                     .Select(e => new Error(
                                     code: $"Validation.{e.PropertyName}",
                                     description: e.ErrorMessage,
                                     type: ErrorType.Validation))
                                     .ToArray();

        var validationError = new ValidationError(errors);
        var result = Result.Failure<T>(validationError);

        await MatchResultAsync(req, result);
        return result;
    }

    protected async Task<HttpResponseData> MatchResultAsync(
     HttpRequestData req,
     Result result)
    {
        return await result.Match(
            onSuccess: async () =>
            {
                var response = req.CreateResponse(HttpStatusCode.NoContent);
                return response;
            },
            onFailure: async error => await CreateProblemResponse(req, error));
    }

    protected async Task<HttpResponseData> MatchResultAsync<T>(
     HttpRequestData req,
     Result<T> result)
    {
        return await result.Match(
            onSuccess: async value =>
            {
                var response = req.CreateResponse(HttpStatusCode.OK);
                await response.WriteAsJsonAsync(value); 
                return response;
            },
            onFailure: async error =>
            {
                return await CreateProblemResponse(req, error);
            });
    }

    private async Task<HttpResponseData> CreateProblemResponse(HttpRequestData req, Result error)
    {
        var problemDetails = CustomResults.ToProblemDetails(error);

        var response = req.CreateResponse();
        await response.WriteAsJsonAsync(problemDetails);
        response.StatusCode = (HttpStatusCode)problemDetails.Status!.Value;
        return response;
    }
}
