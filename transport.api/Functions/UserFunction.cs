using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Azure.Functions.Worker;
using FluentValidation;
using Transport_Api.Extensions;
using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Attributes;
using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Enums;
using System.Net;
using Transport.Infraestructure.Authorization;
using Transport.SharedKernel.Contracts.User;
using Transport_Api.Functions.Base;
using transport_api.Extensions;
using Transport.Domain.Users.Abstraction;

namespace Transport_Api.Functions;

public class UserFunction : FunctionBase
{
    private readonly IUserBusiness _loginBusiness;
    private readonly IValidator<LoginDto> _validator;

    public UserFunction(IUserBusiness loginBusiness, IValidator<LoginDto> validator, IServiceProvider serviceProvider) :
        base(serviceProvider)
    {
        _loginBusiness = loginBusiness;
        _validator = validator;
    }

    [Function("login")]
    [AllowAnonymous]

    [OpenApiOperation(operationId: "login", tags: new[] { "User" }, Summary = "Authentication Login", Description = "Operation Login User Auth", Visibility = OpenApiVisibilityType.Important)]
    [OpenApiRequestBody(contentType: "application/json", bodyType: typeof(LoginDto), Required = true, Description = "Login User in App")]
    [OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "application/json", bodyType: typeof(LoginDto), Summary = "Login User", Description = "test.")]
    public async Task<HttpResponseData> Login(
       [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "login")] HttpRequestData req)
    {
        var login = await req.ReadFromJsonAsync<LoginDto>();

        var ipAddress = req.GetClientIp();
        login = login with { IpAddress = ipAddress };

        var result = await ValidateAndMatchAsync(req, login, GetValidator<LoginDto>())
                           .BindAsync(_loginBusiness.Login);

        return await MatchResultAsync(req, result);
    }

    //[Function("renew-token")]
    //[AllowAnonymous]
    //[OpenApiOperation(operationId: "renew-token", tags: new[] { "User" }, Summary = "Renew JWT Access Token", Description = "Renews access token using refresh token", Visibility = OpenApiVisibilityType.Important)]
    //[OpenApiRequestBody(contentType: "application/json", bodyType: typeof(RenewTokenDto), Required = true, Description = "Refresh token DTO")]
    //[OpenApiResponseWithBody(statusCode: HttpStatusCode.OK, contentType: "application/json", bodyType: typeof(RefreshTokenResponseDto), Summary = "New Access Token", Description = "Returns a new JWT access token and refresh token.")]
    //public async Task<HttpResponseData> RenewToken(
    //  [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "renew-token")] HttpRequestData req)
    //{
    //    var dto = await req.ReadFromJsonAsync<RenewTokenDto>();

    //    var ipAddress = req.GetClientIp();

    //    var result = await _loginBusiness.RenewTokenAsync(dto.Token, ipAddress);

    //    return await MatchResultAsync(req, result);
    //}
}
