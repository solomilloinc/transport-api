using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Azure.Functions.Worker;
using System.Net;
using transport.application.Authentication;
using transport.application.Authorization;
using transport.common;
using transport_api.Infrastructure;
using transport_api.Extensions;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Http.HttpResults;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;

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
        return result.Match(
            onSuccess: () => req.CreateResponse(HttpStatusCode.NoContent),
            onFailure: error => CreateProblemResponse(req, error));
    }

    protected async Task<HttpResponseData> MatchResultAsync<T>(
        HttpRequestData req,
        Result<T> result)
    {
        return result.Match(
            onSuccess: value =>
            {
                var response = req.CreateResponse(HttpStatusCode.OK);
                response.WriteAsJsonAsync(value);
                return response;
            },
            onFailure: error => CreateProblemResponse(req, error));
    }

    private HttpResponseData CreateProblemResponse(HttpRequestData req, Result error)
    {
        var problemDetails = CustomResults.ToProblemDetails(error);

        var response = req.CreateResponse((HttpStatusCode)problemDetails.Status!.Value);
        response.WriteAsJsonAsync(problemDetails); // Esto ya setea el header correcto
        return response;
    }


}
