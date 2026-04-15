using ColdWarHistory.Auth.Application;
using ColdWarHistory.Auth.Infrastructure;
using ColdWarHistory.BuildingBlocks.Api;
using ColdWarHistory.BuildingBlocks.Application;
using ColdWarHistory.BuildingBlocks.Contracts;

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.UseUrls(ServiceEndpoints.Auth);

builder.Services.AddColdWarApiDefaults();
builder.Services.AddGatewayForwardedAuthentication();
builder.Services.AddAuthInfrastructure(builder.Configuration);

var app = builder.Build();

app.UseAuthentication();
app.UseAuthorization();
app.UseRequestAudit("auth");

var api = app.MapGroup(ApiRoutes.Auth);

api.MapPost("/register", async (RegisterRequest request, IAuthService authService, CancellationToken cancellationToken) =>
{
    var result = await authService.RegisterAsync(request, cancellationToken);
    return result.IsSuccess
        ? Results.Ok(result.Value)
        : Results.BadRequest(result.Error);
});

api.MapPost("/login", async (LoginRequest request, IAuthService authService, CancellationToken cancellationToken) =>
{
    var result = await authService.LoginAsync(request, cancellationToken);
    return result.IsSuccess
        ? Results.Ok(result.Value)
        : Results.Unauthorized();
});

api.MapPost("/refresh", async (RefreshTokenRequest request, IAuthService authService, CancellationToken cancellationToken) =>
{
    var result = await authService.RefreshAsync(request, cancellationToken);
    return result.IsSuccess
        ? Results.Ok(result.Value)
        : Results.Unauthorized();
});

api.MapPost("/logout", async (LogoutRequest request, IAuthService authService, CancellationToken cancellationToken) =>
{
    await authService.LogoutAsync(request, cancellationToken);
    return Results.NoContent();
});

api.MapGet("/me", (ICurrentUserAccessor accessor) =>
{
    var currentUser = accessor.GetCurrentUser();
    return currentUser.IsAuthenticated
        ? Results.Ok(new UserInfoResponse(currentUser.UserId!.Value, currentUser.UserName!, string.Empty, currentUser.Roles))
        : Results.Unauthorized();
}).RequireAuthorization();

app.MapPost("/internal/auth/introspect", async (TokenIntrospectionRequest request, IAuthService authService, CancellationToken cancellationToken) =>
{
    var result = await authService.IntrospectAsync(request, cancellationToken);
    return Results.Ok(result);
});

app.MapGet("/health", () => Results.Ok(new { service = "auth", status = "ok" }));

app.MapGet("/openapi/v1.json", () =>
{
    var document = OpenApiDocumentBuilder.Build(
        "ColdWarHistory Auth API",
        "v1",
        [
            new OpenApiEndpointDescription("POST", "/api/auth/register", "Register a new user", "Auth", false, [], [
                new OpenApiField("userName", "string", true, "Unique user name"),
                new OpenApiField("email", "string", true, "User email"),
                new OpenApiField("password", "string", true, "Plain password")
            ], [
                new OpenApiResponseDescription(200, "Tokens issued"),
                new OpenApiResponseDescription(400, "Validation failed")
            ]),
            new OpenApiEndpointDescription("POST", "/api/auth/login", "Authenticate with user name or email", "Auth", false, [], [
                new OpenApiField("userNameOrEmail", "string", true, "User name or email"),
                new OpenApiField("password", "string", true, "Plain password")
            ], [
                new OpenApiResponseDescription(200, "Tokens issued"),
                new OpenApiResponseDescription(401, "Unauthorized")
            ]),
            new OpenApiEndpointDescription("POST", "/api/auth/refresh", "Refresh access token", "Auth", false, [], [
                new OpenApiField("refreshToken", "string", true, "Refresh token")
            ], [
                new OpenApiResponseDescription(200, "Tokens refreshed"),
                new OpenApiResponseDescription(401, "Unauthorized")
            ]),
            new OpenApiEndpointDescription("GET", "/api/auth/me", "Current user profile", "Auth", true, [Roles.User, Roles.Editor, Roles.Admin], [], [
                new OpenApiResponseDescription(200, "Current user"),
                new OpenApiResponseDescription(401, "Unauthorized")
            ])
        ]);

    return Results.Json(document, contentType: "application/json");
});

app.Run();
