using System.IdentityModel.Tokens.Jwt;
using System.Reflection;
using System.Security.Claims;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.Functions.Worker.Middleware;
using Transport.Business.Authentication;
using Transport.Infraestructure.Authentication;
using Transport.Infraestructure.Authorization;

namespace Transport_Api.Middleware;

public class AuthorizationMiddleware : IFunctionsWorkerMiddleware
{
    private readonly IAuthorizationService service;
    private readonly ITokenProvider tokenProvider;

    private static readonly HashSet<string> ExcludedPaths = new()
        {
            "MPWebhook",
            "RenderSwaggerUI",
            "RenderSwaggerDocument"
        };
    public AuthorizationMiddleware(IAuthorizationService service, ITokenProvider tokenProvider)
    {
        this.service = service;
        this.tokenProvider = tokenProvider;
    }
    public async Task Invoke(FunctionContext context, FunctionExecutionDelegate next)
    {
        var functionName = context.FunctionDefinition.Name;

        if (ExcludedPaths.Contains(functionName))
        {
            await next(context);
            return;
        }

        var request = await context.GetHttpRequestDataAsync();
        request.Headers.TryGetValues("Authorization", out var authHeaderValues);
        var bearer = authHeaderValues?.FirstOrDefault();

        var targetMethod = GetTargetFunctionMethod(context);

        if (targetMethod.GetCustomAttribute<AllowAnonymousAttribute>() != null)
        {
            await next(context);
            return;
        }

        var attributes = targetMethod.GetCustomAttributes<AuthorizeAttribute>(true);

        if (attributes.Any() && bearer is null)
        {
            throw new UnauthorizedAccessException("Unauthorized");
        }

        if (bearer is not null && service.CheckAuthorization(bearer, out var claims, attributes.FirstOrDefault()?.Roles))
        {
            var userId = claims?.FindFirst(ClaimTypes.Sid)?.Value ?? claims?.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            var email = claims?.FindFirst(ClaimTypes.Email)?.Value;

            if (int.TryParse(userId, out var parsedId))
            {
                var userContext = context.InstanceServices.GetService(typeof(IUserContext)) as UserContext;
                userContext!.UserId = parsedId;
                userContext.Email = email;
            }

            await next(context);
        }
        else
        {
            throw new UnauthorizedAccessException("Unauthorized");
        }
    }


    public static MethodInfo GetTargetFunctionMethod(FunctionContext context)
    {
        var entryPoint = context.FunctionDefinition.EntryPoint;
        var assemblyPath = context.FunctionDefinition.PathToAssembly;
        var assembly = Assembly.LoadFrom(assemblyPath);
        var typeName = entryPoint.Substring(0, entryPoint.LastIndexOf('.'));
        var type = assembly.GetType(typeName);
        var methodName = entryPoint.Substring(entryPoint.LastIndexOf('.') + 1);
        var method = type.GetMethod(methodName);
        return method;
    }
}