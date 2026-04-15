using ColdWarHistory.BuildingBlocks.Api;
using ColdWarHistory.BuildingBlocks.Application;
using ColdWarHistory.BuildingBlocks.Contracts;
using ColdWarHistory.Progress.Application;
using ColdWarHistory.Progress.Infrastructure;

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.UseUrls(ServiceEndpoints.Progress);

builder.Services.AddColdWarApiDefaults();
builder.Services.AddGatewayForwardedAuthentication();
builder.Services.AddProgressInfrastructure(builder.Configuration);

var app = builder.Build();

app.UseAuthentication();
app.UseAuthorization();
app.UseRequestAudit("progress");

app.MapPost("/internal/events/crypto-operation-recorded", async (CryptoOperationRecordedEvent integrationEvent, IProgressService service, CancellationToken cancellationToken) =>
{
    await service.RecordCryptoOperationAsync(integrationEvent, cancellationToken);
    return Results.Accepted();
});

app.MapPost("/internal/events/challenge-completed", async (ChallengeCompletedEvent integrationEvent, IProgressService service, CancellationToken cancellationToken) =>
{
    await service.RecordChallengeCompletedAsync(integrationEvent, cancellationToken);
    return Results.Accepted();
});

app.MapPost("/internal/events/shift-completed", async (ShiftCompletedEvent integrationEvent, IProgressService service, CancellationToken cancellationToken) =>
{
    await service.RecordShiftCompletedAsync(integrationEvent, cancellationToken);
    return Results.Accepted();
});

var api = app.MapGroup(ApiRoutes.Progress).RequireAuthorization();

api.MapGet("/profile", async (ICurrentUserAccessor accessor, IProgressService service, CancellationToken cancellationToken) =>
{
    var currentUser = accessor.GetCurrentUser();
    var profile = await service.GetProfileAsync(currentUser.UserId!.Value, currentUser.UserName ?? "unknown", cancellationToken);
    return Results.Ok(profile);
});

api.MapGet("/leaderboard", async (IProgressService service, CancellationToken cancellationToken) =>
    Results.Ok(await service.GetLeaderboardAsync(cancellationToken)));

app.MapGet("/health", () => Results.Ok(new { service = "progress", status = "ok" }));

app.MapGet("/openapi/v1.json", () =>
{
    var document = OpenApiDocumentBuilder.Build(
        "ColdWarHistory Progress API",
        "v1",
        [
            new OpenApiEndpointDescription("GET", "/api/progress/profile", "Get profile with operations, achievements and metrics", "Progress", true, [Roles.User, Roles.Editor, Roles.Admin], [], [
                new OpenApiResponseDescription(200, "Profile")
            ]),
            new OpenApiEndpointDescription("GET", "/api/progress/leaderboard", "Get leaderboard", "Progress", true, [Roles.User, Roles.Editor, Roles.Admin], [], [
                new OpenApiResponseDescription(200, "Leaderboard")
            ])
        ]);

    return Results.Json(document, contentType: "application/json");
});

app.Run();
