using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using transport.common;

namespace transport_api.Infrastructure;

public static class CustomResults
{
    public static ProblemDetails ToProblemDetails(Result result)
    {
        if (result.IsSuccess)
        {
            throw new InvalidOperationException();
        }

        var problemDetails = new ProblemDetails
        {
            Title = GetTitle(result.Error),
            Detail = GetDetail(result.Error),
            Type = GetType(result.Error.Type),
            Status = GetStatusCode(result.Error.Type)
        };

        var errors = GetErrors(result);
        if (errors is not null)
        {
            foreach (var pair in errors)
            {
                problemDetails.Extensions[pair.Key] = pair.Value;
            }
        }

        return problemDetails;
    }

    public static IResult Problem(Result result)
    {
        var details = ToProblemDetails(result);
        return Results.Problem(
            title: details.Title,
            detail: details.Detail,
            type: details.Type,
            statusCode: details.Status,
            extensions: details.Extensions);
    }

    private static string GetTitle(Error error) =>
        error.Type switch
        {
            ErrorType.Validation => error.Code,
            ErrorType.Problem => error.Code,
            ErrorType.NotFound => error.Code,
            ErrorType.Conflict => error.Code,
            _ => "Server failure"
        };

    private static string GetDetail(Error error) =>
        error.Type switch
        {
            ErrorType.Validation => error.Description,
            ErrorType.Problem => error.Description,
            ErrorType.NotFound => error.Description,
            ErrorType.Conflict => error.Description,
            _ => "An unexpected error occurred"
        };

    private static string GetType(ErrorType errorType) =>
        errorType switch
        {
            ErrorType.Validation => "https://tools.ietf.org/html/rfc7231#section-6.5.1",
            ErrorType.Problem => "https://tools.ietf.org/html/rfc7231#section-6.5.1",
            ErrorType.NotFound => "https://tools.ietf.org/html/rfc7231#section-6.5.4",
            ErrorType.Conflict => "https://tools.ietf.org/html/rfc7231#section-6.5.8",
            _ => "https://tools.ietf.org/html/rfc7231#section-6.6.1"
        };

    private static int GetStatusCode(ErrorType errorType) =>
        errorType switch
        {
            ErrorType.Validation => StatusCodes.Status400BadRequest,
            ErrorType.NotFound => StatusCodes.Status404NotFound,
            ErrorType.Conflict => StatusCodes.Status409Conflict,
            _ => StatusCodes.Status500InternalServerError
        };

    private static Dictionary<string, object?>? GetErrors(Result result)
    {
        if (result.Error is not ValidationError validationError)
        {
            return null;
        }

        return new Dictionary<string, object?>
        {
            { "errors", validationError.Errors }
        };
    }

}
