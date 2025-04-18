﻿using Microsoft.Azure.Functions.Worker.Http;
using Microsoft.Azure.Functions.Worker;
using FluentValidation;
using Transport_Api.Extensions;
using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Attributes;
using Microsoft.Azure.WebJobs.Extensions.OpenApi.Core.Enums;
using System.Net;
using Transport.Infraestructure.Authorization;
using Transport.SharedKernel.Contracts.User;
using Transport.Domain.Users.Abstraction;
using Transport_Api.Functions.Base;

namespace Transport_Api.Functions;

public class UserFunction : FunctionBase
{
    private readonly ILoginBusiness _loginBusiness;
    private readonly IValidator<LoginDto> _validator;

    public UserFunction(ILoginBusiness loginBusiness, IValidator<LoginDto> validator, IServiceProvider serviceProvider) :
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

        var result = await ValidateAndMatchAsync(req, login, GetValidator<LoginDto>())
                           .BindAsync(_loginBusiness.Login);

        return await MatchResultAsync(req, result);
    }
}
