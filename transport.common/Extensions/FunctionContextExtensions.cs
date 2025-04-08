using System.Security.Claims;
using Microsoft.Azure.Functions.Worker;

namespace transport.common.Extensions;

public static class FunctionContextExtensions
{
    public static ClaimsPrincipal GetUserPrincipal(this FunctionContext context)
    {
        context.Items.TryGetValue("User", out var user);
        return user as ClaimsPrincipal ?? new ClaimsPrincipal();
    }
}
