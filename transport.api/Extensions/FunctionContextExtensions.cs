using System.Security.Claims;
using Microsoft.Azure.Functions.Worker;

namespace Transport_Api.Extensions;

public static class FunctionContextExtensions
{
    public static ClaimsPrincipal GetUserPrincipal(this FunctionContext context)
    {
        context.Items.TryGetValue("User", out var user);
        return user as ClaimsPrincipal ?? new ClaimsPrincipal();
    }
}
