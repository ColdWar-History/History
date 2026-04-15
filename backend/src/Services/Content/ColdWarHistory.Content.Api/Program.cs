using ColdWarHistory.BuildingBlocks.Api;
using ColdWarHistory.BuildingBlocks.Contracts;
using ColdWarHistory.Content.Application;
using ColdWarHistory.Content.Infrastructure;

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.UseUrls(ServiceEndpoints.Content);

builder.Services.AddColdWarApiDefaults();
builder.Services.AddGatewayForwardedAuthentication();
builder.Services.AddContentInfrastructure();

var app = builder.Build();

app.UseAuthentication();
app.UseAuthorization();
app.UseRequestAudit("content");

var api = app.MapGroup(ApiRoutes.Content);

api.MapGet("/ciphers", async (string? search, string? category, string? era, bool publishedOnly, IContentService service, CancellationToken cancellationToken) =>
    Results.Ok(await service.GetCiphersAsync(search, category, era, publishedOnly, cancellationToken)));

api.MapGet("/ciphers/{id:guid}", async (Guid id, IContentService service, CancellationToken cancellationToken) =>
{
    var result = await service.GetCipherAsync(id, cancellationToken);
    return result is null ? Results.NotFound() : Results.Ok(result);
});

api.MapPost("/ciphers", async (UpsertCipherCardRequest request, IContentService service, CancellationToken cancellationToken) =>
    Results.Ok(await service.UpsertCipherAsync(null, request, cancellationToken)))
    .RequireAuthorization(policy => policy.RequireRole(Roles.Editor, Roles.Admin));

api.MapPut("/ciphers/{id:guid}", async (Guid id, UpsertCipherCardRequest request, IContentService service, CancellationToken cancellationToken) =>
    Results.Ok(await service.UpsertCipherAsync(id, request, cancellationToken)))
    .RequireAuthorization(policy => policy.RequireRole(Roles.Editor, Roles.Admin));

api.MapPost("/ciphers/{id:guid}/publication/{status}", async (Guid id, string status, IContentService service, CancellationToken cancellationToken) =>
{
    await service.PublishCipherAsync(id, status, cancellationToken);
    return Results.NoContent();
}).RequireAuthorization(policy => policy.RequireRole(Roles.Editor, Roles.Admin));

api.MapGet("/events", async (string? region, int? year, string? topic, bool publishedOnly, IContentService service, CancellationToken cancellationToken) =>
    Results.Ok(await service.GetEventsAsync(region, year, topic, publishedOnly, cancellationToken)));

api.MapGet("/events/{id:guid}", async (Guid id, IContentService service, CancellationToken cancellationToken) =>
{
    var result = await service.GetEventAsync(id, cancellationToken);
    return result is null ? Results.NotFound() : Results.Ok(result);
});

api.MapPost("/events", async (UpsertHistoricalEventRequest request, IContentService service, CancellationToken cancellationToken) =>
    Results.Ok(await service.UpsertEventAsync(null, request, cancellationToken)))
    .RequireAuthorization(policy => policy.RequireRole(Roles.Editor, Roles.Admin));

api.MapPut("/events/{id:guid}", async (Guid id, UpsertHistoricalEventRequest request, IContentService service, CancellationToken cancellationToken) =>
    Results.Ok(await service.UpsertEventAsync(id, request, cancellationToken)))
    .RequireAuthorization(policy => policy.RequireRole(Roles.Editor, Roles.Admin));

api.MapPost("/events/{id:guid}/publication/{status}", async (Guid id, string status, IContentService service, CancellationToken cancellationToken) =>
{
    await service.PublishEventAsync(id, status, cancellationToken);
    return Results.NoContent();
}).RequireAuthorization(policy => policy.RequireRole(Roles.Editor, Roles.Admin));

api.MapGet("/collections", async (bool publishedOnly, IContentService service, CancellationToken cancellationToken) =>
    Results.Ok(await service.GetCollectionsAsync(publishedOnly, cancellationToken)));

api.MapGet("/collections/{id:guid}", async (Guid id, IContentService service, CancellationToken cancellationToken) =>
{
    var result = await service.GetCollectionAsync(id, cancellationToken);
    return result is null ? Results.NotFound() : Results.Ok(result);
});

api.MapPost("/collections", async (UpsertCollectionRequest request, IContentService service, CancellationToken cancellationToken) =>
    Results.Ok(await service.UpsertCollectionAsync(null, request, cancellationToken)))
    .RequireAuthorization(policy => policy.RequireRole(Roles.Editor, Roles.Admin));

api.MapPut("/collections/{id:guid}", async (Guid id, UpsertCollectionRequest request, IContentService service, CancellationToken cancellationToken) =>
    Results.Ok(await service.UpsertCollectionAsync(id, request, cancellationToken)))
    .RequireAuthorization(policy => policy.RequireRole(Roles.Editor, Roles.Admin));

api.MapPost("/collections/{id:guid}/publication/{status}", async (Guid id, string status, IContentService service, CancellationToken cancellationToken) =>
{
    await service.PublishCollectionAsync(id, status, cancellationToken);
    return Results.NoContent();
}).RequireAuthorization(policy => policy.RequireRole(Roles.Editor, Roles.Admin));

app.MapGet("/health", () => Results.Ok(new { service = "content", status = "ok" }));

app.MapGet("/openapi/v1.json", () =>
{
    var document = OpenApiDocumentBuilder.Build(
        "ColdWarHistory Content API",
        "v1",
        [
            new OpenApiEndpointDescription("GET", "/api/content/ciphers", "List cipher cards with filters", "Content", false, [], [], [new OpenApiResponseDescription(200, "Cipher cards")]),
            new OpenApiEndpointDescription("POST", "/api/content/ciphers", "Create cipher card", "Content", true, [Roles.Editor, Roles.Admin], [
                new OpenApiField("code", "string", true, "Cipher code"),
                new OpenApiField("name", "string", true, "Title"),
                new OpenApiField("category", "string", true, "Category")
            ], [new OpenApiResponseDescription(200, "Cipher card saved")]),
            new OpenApiEndpointDescription("GET", "/api/content/events", "List timeline events with filters", "Content", false, [], [], [new OpenApiResponseDescription(200, "Timeline events")]),
            new OpenApiEndpointDescription("GET", "/api/content/collections", "List thematic collections", "Content", false, [], [], [new OpenApiResponseDescription(200, "Collections")])
        ]);

    return Results.Json(document, contentType: "application/json");
});

app.Run();
