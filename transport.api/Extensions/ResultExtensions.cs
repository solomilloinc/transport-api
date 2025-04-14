using Transport.SharedKernel;

namespace Transport_Api.Extensions;

public static class ResultExtensions
{
    public static TOut Match<TOut>(
    this Result result,
    Func<TOut> onSuccess,
    Func<Result, TOut> onFailure)
    {
        return result.IsSuccess ? onSuccess() : onFailure(result);
    }

    public static TOut Match<TIn, TOut>(
        this Result<TIn> result,
        Func<TIn, TOut> onSuccess,
        Func<Result<TIn>, TOut> onFailure)
    {
        return result.IsSuccess ? onSuccess(result.Value) : onFailure(result);
    }

    public static async Task<Result<TOut>> BindAsync<TIn, TOut>(
      this Task<Result<TIn>> resultTask,
      Func<TIn, Task<Result<TOut>>?> func)
    {
        var result = await resultTask;
        return result.IsFailure ? Result.Failure<TOut>(result.Error) : await func(result.Value);
    }

    public static async Task<Result<TOut>> BindAsync<TIn, TOut>(
        this Task<Result<TIn>> resultTask,
        Func<TIn, Result<TOut>> func)
    {
        var result = await resultTask;
        return result.IsFailure ? Result.Failure<TOut>(result.Error) : func(result.Value);
    }
}
