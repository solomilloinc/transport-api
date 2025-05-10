using Microsoft.Azure.Functions.Worker.Http;

namespace transport_api.Extensions;

public static class HttpRequestDataExtensions
{
    public static string GetClientIp(this HttpRequestData req)
    {
        if (req.Headers.TryGetValues("X-Forwarded-For", out var values))
        {
            return values.FirstOrDefault()?.Split(',').FirstOrDefault()?.Trim() ?? "unknown";
        }

        return req.FunctionContext?.BindingContext?.BindingData.TryGetValue("Headers", out var headerObj) == true &&
               headerObj is IReadOnlyDictionary<string, object> headers &&
               headers.TryGetValue("X-Forwarded-For", out var ip)
            ? ip?.ToString()
            : "unknown";
    }
}
