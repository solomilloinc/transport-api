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

namespace transport_api.Functions;

public class TokenFunction : FunctionBase
{
    private readonly UserBusiness _userBusiness;

    public TokenFunction(UserBusiness userBusiness, IServiceProvider serviceProvider) : base(serviceProvider)
    {
        _userBusiness = userBusiness;
    }

    [Function("renew-token")]
    [AllowAnonymous]
    [OpenApiOperation(operationId: "renew-token", tags: new[] { "User" }, Summary = "Renew Refresh Token", Description = "Renews the refresh token and provides a new access token.", Visibility = OpenApiVisibilityType.Important)]
    [OpenApiRequestBody(contentType: "application/json", bodyType: typeof(RenewTokenDto), Required = true, Description = "Request body with the refresh token.")]
    [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "application/json", bodyType: typeof(RefreshTokenResponseDto), Summary = "New Tokens", Description = "The new access token and refresh token.")]
    public async Task<HttpResponseData> RenewToken(
        [HttpTrigger(AuthorizationLevel.Function, "post", Route = "renew-token")] HttpRequestData req)
    {
        var renewTokenDto = await req.ReadFromJsonAsync<RenewTokenDto>();

        var result = await _userBusiness.RenewTokenAsync(renewTokenDto.Token, req.GetClientIp());

        return await MatchResultAsync(req, result);
    }
}
