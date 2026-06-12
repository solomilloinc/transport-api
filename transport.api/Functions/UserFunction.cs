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
using Transport.Business.Authentication;
using Transport.SharedKernel;
using Transport.Domain.Users;
using Microsoft.OpenApi.Models;

namespace Transport_Api.Functions;

public class UserFunction : FunctionBase
{
    private readonly IUserBusiness _userBusiness;
    private readonly IUserContext _userContext;
    private readonly IGoogleTokenValidator _googleTokenValidator;

    public UserFunction(
        IUserBusiness loginBusiness,
        IUserContext userContext,
        IGoogleTokenValidator googleTokenValidator,
        IServiceProvider serviceProvider) :
        base(serviceProvider)
    {
        _userBusiness = loginBusiness;
        _userContext = userContext;
        _googleTokenValidator = googleTokenValidator;
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

        return await CreateTokenResponseAsync(req, result.Value);
    }

    [Function("client-register")]
    [OpenApiOperation(operationId: "ClientRegister", tags: new[] { "Auth" }, Summary = "Registro manual de cliente", Description = "Crea User + Customer y devuelve JWT + refresh token")]
    [OpenApiRequestBody("application/json", typeof(ClientRegisterRequestDto), Description = "Datos de registro del cliente", Required = true)]
    [OpenApiResponseWithBody(HttpStatusCode.OK, "application/json", typeof(object), Description = "Access token issued")]
    [AllowAnonymous]
    public async Task<HttpResponseData> RegisterClient(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "client-register")] HttpRequestData req)
    {
        var dto = await req.ReadFromJsonAsync<ClientRegisterRequestDto>();
        dto = dto with { IpAddress = req.GetClientIp() };

        var result = await ValidateAndMatchAsync(req, dto, GetValidator<ClientRegisterRequestDto>())
            .BindAsync(_userBusiness.RegisterClientAsync);

        if (!result.IsSuccess)
            return await MatchResultAsync(req, result);

        return await CreateTokenResponseAsync(req, result.Value);
    }

    [Function("google-login")]
    [OpenApiOperation(operationId: "GoogleLogin", tags: new[] { "Auth" }, Summary = "Login Google cliente", Description = "Valida Google, crea/vincula User + Customer y devuelve JWT + refresh token")]
    [OpenApiRequestBody("application/json", typeof(GoogleLoginRequestDto), Description = "Google id token", Required = true)]
    [OpenApiResponseWithBody(HttpStatusCode.OK, "application/json", typeof(object), Description = "Access token issued")]
    [AllowAnonymous]
    public async Task<HttpResponseData> GoogleLogin(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "google-login")] HttpRequestData req)
    {
        var dto = await req.ReadFromJsonAsync<GoogleLoginRequestDto>();
        dto = dto with { IpAddress = req.GetClientIp() };

        var validation = await ValidateAndMatchAsync(req, dto, GetValidator<GoogleLoginRequestDto>());
        if (!validation.IsSuccess)
            return await MatchResultAsync(req, validation);

        GoogleAuthenticatedUserDto googleIdentity;
        try
        {
            var validated = await _googleTokenValidator.ValidateAsync(dto.IdToken);
            googleIdentity = validated with { IpAddress = dto.IpAddress };
        }
        catch (InvalidOperationException ex)
        {
            return await MatchResultAsync(req, Result.Failure<LoginResponseDto>(Error.Validation("User.GoogleLogin", ex.Message)));
        }
        catch (Exception)
        {
            return await MatchResultAsync(req, Result.Failure<LoginResponseDto>(Error.Problem("User.GoogleLogin", "No se pudo validar la identidad de Google.")));
        }

        var result = await _userBusiness.LoginWithGoogleAsync(googleIdentity);
        if (!result.IsSuccess)
            return await MatchResultAsync(req, result);

        return await CreateTokenResponseAsync(req, result.Value);
    }

    [Function("logout")]
    [Authorize(["Admin", "User", "Client", "SuperAdmin"])]
    [OpenApiOperation(operationId: "Logout", tags: new[] { "Auth" }, Summary = "Cerrar sesión", Description = "Revoca el token de actualización y elimina la cookie.")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.OK, Summary = "Logout exitoso", Description = "La sesión fue cerrada correctamente.")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.Unauthorized, Summary = "No autorizado", Description = "El token de actualización es inválido o no fue proporcionado.")]
    public async Task<HttpResponseData> Logout(
    [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "logout")] HttpRequestData req)
    {
        var refreshToken = req.GetCookieValue("refreshToken");

        if (!string.IsNullOrWhiteSpace(refreshToken))
        {
            var ipAddress = req.GetClientIp();
            await _userBusiness.LogoutAsync(refreshToken, ipAddress);
        }

        var response = req.CreateResponse(HttpStatusCode.OK);
        await response.WriteStringAsync("Logged out");

        response.Headers.Add("Set-Cookie",
            $"refreshToken=; HttpOnly; Secure; SameSite=None; Path=/; Expires=Thu, 01 Jan 1970 00:00:00 GMT");

        return response;
    }

    [Function("revoke-all-sessions")]
    [Authorize(["Admin", "User", "Client", "SuperAdmin"])]
    [OpenApiOperation(operationId: "RevokeAllSessions", tags: new[] { "Auth" }, Summary = "Cerrar todas las sesiones", Description = "Revoca todos los refresh tokens del usuario actual.")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.OK, Summary = "Sesiones revocadas", Description = "Todas las sesiones fueron cerradas correctamente.")]
    [OpenApiResponseWithoutBody(statusCode: HttpStatusCode.Unauthorized, Summary = "No autorizado", Description = "El usuario no está autenticado.")]
    public async Task<HttpResponseData> RevokeAllSessions(
    [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "revoke-all-sessions")] HttpRequestData req)
    {
        var ipAddress = req.GetClientIp();
        await _userBusiness.RevokeAllSessionsAsync(_userContext.UserId, ipAddress);

        var response = req.CreateResponse(HttpStatusCode.OK);
        await response.WriteStringAsync("All sessions revoked");

        response.Headers.Add("Set-Cookie",
            $"refreshToken=; HttpOnly; Secure; SameSite=None; Path=/; Expires=Thu, 01 Jan 1970 00:00:00 GMT");

        return response;
    }

    [Function("me")]
    [Authorize(["Admin", "User", "Client", "SuperAdmin"])]
    [OpenApiOperation(operationId: "Me", tags: new[] { "Auth" }, Summary = "Perfil actual", Description = "Obtiene el perfil del usuario autenticado.")]
    [OpenApiResponseWithBody(HttpStatusCode.OK, "application/json", typeof(CurrentUserProfileDto), Description = "Perfil actual")]
    public async Task<HttpResponseData> Me(
        [HttpTrigger(AuthorizationLevel.Anonymous, "get", Route = "me")] HttpRequestData req)
    {
        var result = await _userBusiness.GetCurrentProfileAsync(_userContext.UserId);
        return await MatchResultAsync(req, result);
    }

    [Function("client-profile-complete")]
    [Authorize(["Client"])]
    [OpenApiOperation(operationId: "ClientProfileComplete", tags: new[] { "Auth" }, Summary = "Completar perfil cliente", Description = "Completa el onboarding del cliente autenticado.")]
    [OpenApiRequestBody("application/json", typeof(ClientProfileCompleteRequestDto), Description = "Datos obligatorios de perfil", Required = true)]
    [OpenApiResponseWithBody(HttpStatusCode.OK, "application/json", typeof(CurrentUserProfileDto), Description = "Perfil actualizado")]
    public async Task<HttpResponseData> CompleteClientProfile(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "client-profile-complete")] HttpRequestData req)
    {
        var dto = await req.ReadFromJsonAsync<ClientProfileCompleteRequestDto>();
        var result = await ValidateAndMatchAsync(req, dto, GetValidator<ClientProfileCompleteRequestDto>())
            .BindAsync(x => _userBusiness.CompleteClientProfileAsync(_userContext.UserId, x));

        return await MatchResultAsync(req, result);
    }

    [Function("operative-user-create")]
    [Authorize(["Admin"])]
    [OpenApiOperation(operationId: "OperativeUserCreate", tags: new[] { "User" }, Summary = "Crear usuario operativo", Description = "Crea un nuevo usuario operativo.")]
    [OpenApiRequestBody("application/json", typeof(UserCreateRequestDto), Description = "Datos del usuario operativo", Required = true)]
    [OpenApiResponseWithBody(HttpStatusCode.OK, "application/json", typeof(int), Description = "UserId creado")]
    public async Task<HttpResponseData> CreateOperativeUser(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "operative-user-create")] HttpRequestData req)
    {
        var dto = await req.ReadFromJsonAsync<UserCreateRequestDto>();
        var result = await ValidateAndMatchAsync(req, dto, GetValidator<UserCreateRequestDto>())
            .BindAsync(_userBusiness.CreateOperativeAsync);

        return await MatchResultAsync(req, result);
    }

    [Function("operative-user-update")]
    [Authorize(["Admin"])]
    [OpenApiOperation(operationId: "OperativeUserUpdate", tags: new[] { "User" }, Summary = "Actualizar usuario operativo", Description = "Actualiza email, rol permitido y estado.")]
    [OpenApiParameter("userId", In = ParameterLocation.Path, Required = true, Type = typeof(int), Description = "User ID")]
    [OpenApiRequestBody("application/json", typeof(UserUpdateRequestDto), Description = "Datos del usuario operativo", Required = true)]
    [OpenApiResponseWithoutBody(HttpStatusCode.OK, Description = "Usuario actualizado")]
    public async Task<HttpResponseData> UpdateOperativeUser(
        [HttpTrigger(AuthorizationLevel.Anonymous, "put", Route = "operative-user-update/{userId:int}")] HttpRequestData req,
        int userId)
    {
        var dto = await req.ReadFromJsonAsync<UserUpdateRequestDto>();
        var result = await ValidateAndMatchAsync(req, dto, GetValidator<UserUpdateRequestDto>())
            .BindAsync(x => _userBusiness.UpdateOperativeAsync(userId, x));

        return await MatchResultAsync(req, result);
    }

    [Function("operative-user-report")]
    [Authorize(["Admin"])]
    [OpenApiOperation(operationId: "OperativeUserReport", tags: new[] { "User" }, Summary = "Listado de usuarios operativos", Description = "Devuelve la lista paginada de usuarios operativos.")]
    [OpenApiRequestBody("application/json", typeof(PagedReportRequestDto<UserReportFilterRequestDto>), Description = "Filtros", Required = true)]
    [OpenApiResponseWithBody(HttpStatusCode.OK, "application/json", typeof(PagedReportResponseDto<UserReportItemDto>), Description = "Listado paginado")]
    public async Task<HttpResponseData> GetOperativeUserReport(
        [HttpTrigger(AuthorizationLevel.Anonymous, "post", Route = "operative-user-report")] HttpRequestData req)
    {
        var dto = await req.ReadFromJsonAsync<PagedReportRequestDto<UserReportFilterRequestDto>>();
        var result = await _userBusiness.GetOperativeUsersReportAsync(dto);
        return await MatchResultAsync(req, result);
    }

    private async Task<HttpResponseData> CreateTokenResponseAsync(HttpRequestData req, LoginResponseDto login)
    {
        var response = req.CreateResponse(HttpStatusCode.OK);
        await response.WriteAsJsonAsync(new { login.AccessToken, login.RefreshToken });

        var encodedToken = WebUtility.UrlEncode(login.RefreshToken);
        response.Headers.Add("Set-Cookie",
            $"refreshToken={encodedToken}; HttpOnly; Secure; SameSite=None; Path=/; Max-Age=604800");

        return response;
    }
}
