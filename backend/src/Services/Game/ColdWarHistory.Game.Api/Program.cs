using ColdWarHistory.BuildingBlocks.Api;
using ColdWarHistory.BuildingBlocks.Application;
using ColdWarHistory.BuildingBlocks.Contracts;
using ColdWarHistory.Game.Application;
using ColdWarHistory.Game.Infrastructure;

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.UseUrls(ServiceEndpoints.Game);

builder.Services.AddColdWarApiDefaults();
builder.Services.AddGatewayForwardedAuthentication();
builder.Services.AddGameInfrastructure(builder.Configuration);

var app = builder.Build();

app.UseAuthentication();
app.UseAuthorization();
app.UseRequestAudit("game");

var api = app.MapGroup(ApiRoutes.Game);

api.MapPost("/training/generate", async (string cipherCode, string difficulty, IGameService service, CancellationToken cancellationToken) =>
    Results.Ok(await service.GenerateTrainingChallengeAsync(cipherCode, difficulty, cancellationToken)));

api.MapPost("/training/{challengeId:guid}/submit", async (Guid challengeId, SubmitTrainingAnswerRequest request, IGameService service, ICurrentUserAccessor currentUserAccessor, CancellationToken cancellationToken) =>
    Results.Ok(await service.SubmitChallengeAsync(challengeId, request, currentUserAccessor.GetCurrentUser(), cancellationToken)));

api.MapGet("/daily", async (IGameService service, CancellationToken cancellationToken) =>
    Results.Ok(await service.GetDailyChallengeAsync(cancellationToken)));

api.MapPost("/shifts/start", async (StartShiftRequest request, IGameService service, CancellationToken cancellationToken) =>
    Results.Ok(await service.StartShiftAsync(request.Difficulty, cancellationToken)));

api.MapPost("/shifts/{shiftId:guid}/resolve", async (Guid shiftId, ResolveShiftMessageRequest request, IGameService service, ICurrentUserAccessor currentUserAccessor, CancellationToken cancellationToken) =>
    Results.Ok(await service.ResolveShiftMessageAsync(shiftId, request, currentUserAccessor.GetCurrentUser(), cancellationToken)));

api.MapGet("/shifts/{shiftId:guid}/report", async (Guid shiftId, IGameService service, ICurrentUserAccessor currentUserAccessor, CancellationToken cancellationToken) =>
{
    var report = await service.GetShiftReportAsync(shiftId, currentUserAccessor.GetCurrentUser(), cancellationToken);
    return report is null ? Results.Accepted() : Results.Ok(report);
});

app.MapGet("/health", () => Results.Ok(new { service = "game", status = "ok" }));

app.MapGet("/openapi/v1.json", () =>
{
    var document = OpenApiDocumentBuilder.Build(
        "ColdWarHistory Game API",
        "v1",
        [
            new OpenApiEndpointDescription("POST", "/api/game/training/generate", "Generate a training challenge", "Game", false, [], [
                new OpenApiField("cipherCode", "string", true, "Cipher code"),
                new OpenApiField("difficulty", "string", true, "Challenge difficulty")
            ], [new OpenApiResponseDescription(200, "Challenge generated")]),
            new OpenApiEndpointDescription("POST", "/api/game/training/{challengeId}/submit", "Submit a training answer", "Game", false, [], [
                new OpenApiField("answer", "string", true, "User answer"),
                new OpenApiField("usedHint", "boolean", true, "Whether hint was used")
            ], [new OpenApiResponseDescription(200, "Attempt evaluated")]),
            new OpenApiEndpointDescription("GET", "/api/game/daily", "Get daily challenge", "Game", false, [], [], [new OpenApiResponseDescription(200, "Daily challenge")]),
            new OpenApiEndpointDescription("POST", "/api/game/shifts/start", "Start inspector shift", "Game", false, [], [
                new OpenApiField("difficulty", "string", true, "Shift difficulty")
            ], [new OpenApiResponseDescription(200, "Shift started")])
        ]);

    return Results.Json(document, contentType: "application/json");
});

app.Run();
