using Microsoft.AspNetCore.Builder;
using Transport_Api.Middleware;

namespace Transport_Api.Extensions;

public static class MiddlewareExtensions
{
    public static IApplicationBuilder UseFunctionContextMiddleware(this IApplicationBuilder app)
    {
        app.UseMiddleware<ExceptionHandlingMiddleware>();

        app.UseMiddleware<AuthorizationMiddleware>();

        return app;
    }
}
