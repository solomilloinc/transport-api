using Microsoft.AspNetCore.Builder;
using transport_api.Middleware;

namespace transport_api.Extensions;

public static class MiddlewareExtensions
{
    public static IApplicationBuilder UseFunctionContextMiddleware(this IApplicationBuilder app)
    {
        app.UseMiddleware<ExceptionHandlingMiddleware>();

        app.UseMiddleware<AuthorizationMiddleware>();

        return app;
    }
}
