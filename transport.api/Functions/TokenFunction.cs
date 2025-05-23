﻿using System;
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
    [OpenApiOperation(operationId: "RenewToken", tags: new[] { "Auth" }, Summary = "Renew access token", Description = "Renews JWT using refresh token from cookie")]
    [OpenApiResponseWithBody(HttpStatusCode.OK, "application/json", typeof(object), Description = "New access token issued")]
    [OpenApiResponseWithoutBody(HttpStatusCode.Unauthorized, Description = "Refresh token missing or invalid")]
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

        var encodedToken = WebUtility.UrlEncode(result.Value.RefreshToken);

        response.Headers.Add("Set-Cookie",
            $"refreshToken={encodedToken}; HttpOnly; Secure; SameSite=Strict; Path=/; Max-Age=604800");

        return response;
    }  


}
