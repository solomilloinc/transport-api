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

    public static string? GetCookieValue(this HttpRequestData req, string cookieName)
    {
        var cookieHeader = req.Headers.GetValues("Cookie").FirstOrDefault();
        return cookieHeader?
            .Split(';')
            .Select(c => c.Trim())
            .FirstOrDefault(c => c.StartsWith($"{cookieName}="))
            ?.Split('=')[1];
    }
}
