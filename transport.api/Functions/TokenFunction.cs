using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Attributes;
using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Enums;
using Transport.Business.UserBusiness;
using Transport.SharedKernel.Contracts.User;
using Transport_Api.Functions.Base;
using Transport.Infraestructure.Authorization;
using transport_api.Extensions;
using Transport.Domain.Users.Abstraction;

namespace transport_api.Functions;

public class TokenFunction : FunctionBase
{
    private readonly IUserBusiness _userBusiness;

    public TokenFunction(IUserBusiness userBusiness, IServiceProvider serviceProvider) : base(serviceProvider)
    {
        _userBusiness = userBusiness;
    }

    [Function("renew-token")]
    [AllowAnonymous]
    public async Task<HttpResponseData> RenewToken(
    [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "renew-token")] HttpRequestData req)
    {
        var refreshToken = WebUtility.UrlDecode(req.GetCookieValue("refreshToken"));

        if (string.IsNullOrWhiteSpace(refreshToken))
            return req.CreateResponse(HttpStatusCode.Unauthorized);

        var result = await _userBusiness.RenewTokenAsync(refreshToken, req.GetClientIp());

        if (!result.IsSuccess)
            return await MatchResultAsync(req, result);

        var response = req.CreateResponse(HttpStatusCode.OK);
        await response.WriteAsJsonAsync(new { token = result.Value.AccessToken });

        response.Headers.Add("Set-Cookie",
            $"refreshToken={result.Value.RefreshToken}; HttpOnly; Secure; SameSite=Strict; Path=/; Max-Age=604800");

        return response;
    }

    [Function("logout")]
    [Authorize]
    public async Task<HttpResponseData> Logout(
    [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "logout")] HttpRequestData req)
    {
        var cookieHeader = req.Headers.GetValues("Cookie").FirstOrDefault();
        var refreshToken = req.GetCookieValue("refreshToken");

        if (!string.IsNullOrWhiteSpace(refreshToken))
        {
            var ipAddress = req.GetClientIp();
            await _userBusiness.LogoutAsync(refreshToken, ipAddress);
        }

        var response = req.CreateResponse(HttpStatusCode.OK);
        await response.WriteStringAsync("Logged out");

        response.Headers.Add("Set-Cookie",
            $"refreshToken=; HttpOnly; Secure; SameSite=Strict; Path=/; Expires=Thu, 01 Jan 1970 00:00:00 GMT");

        return response;
    }


}
