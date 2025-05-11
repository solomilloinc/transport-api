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
    private readonly IUserBusiness _userBusiness;
    private readonly IValidator<LoginDto> _validator;

    public UserFunction(IUserBusiness loginBusiness, IValidator<LoginDto> validator, IServiceProvider serviceProvider) :
        base(serviceProvider)
    {
        _userBusiness = loginBusiness;
        _validator = validator;
    }

    [Function("login")]
    [OpenApiOperation(operationId: "Login", tags: new[] { "Auth" }, Summary = "Login user", Description = "Performs login and returns JWT + refresh token")]
    [OpenApiRequestBody("application/json", typeof(LoginDto), Description = "User login credentials", Required = true)]
    [OpenApiResponseWithBody(HttpStatusCode.OK, "application/json", typeof(object), Description = "Access token issued")]
    [OpenApiResponseWithoutBody(HttpStatusCode.Unauthorized, Description = "Invalid credentials")]
    [AllowAnonymous]
    public async Task<HttpResponseData> Login(
    [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "login")] HttpRequestData req)
    {
        var login = await req.ReadFromJsonAsync<LoginDto>();
        login = login with { IpAddress = req.GetClientIp() };

        var result = await ValidateAndMatchAsync(req, login, GetValidator<LoginDto>())
                           .BindAsync(_userBusiness.Login);

        if (!result.IsSuccess)
            return await MatchResultAsync(req, result);

        var response = req.CreateResponse(HttpStatusCode.OK);
        await response.WriteAsJsonAsync(new { result.Value.AccessToken });

        var encodedToken = WebUtility.UrlEncode(result.Value.RefreshToken);

        response.Headers.Add("Set-Cookie",
            $"refreshToken={encodedToken}; HttpOnly; Secure; SameSite=Strict; Path=/; Max-Age=604800");

        return response;
    }
}
