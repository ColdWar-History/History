using ColdWarHistory.BuildingBlocks.Api;
using ColdWarHistory.BuildingBlocks.Application;
using ColdWarHistory.BuildingBlocks.Contracts;
using ColdWarHistory.Crypto.Application;
using ColdWarHistory.Crypto.Infrastructure;

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.UseUrls(ServiceEndpoints.Crypto);

builder.Services.AddColdWarApiDefaults();
builder.Services.AddGatewayForwardedAuthentication();
builder.Services.AddCryptoInfrastructure();

var app = builder.Build();

app.UseAuthentication();
app.UseAuthorization();
app.UseRequestAudit("crypto");

var api = app.MapGroup(ApiRoutes.Crypto);

api.MapGet("/catalog", (ICryptoCatalog catalog) => Results.Ok(catalog.GetAll()));

api.MapPost("/transform", async (CryptoTransformRequest request, ICryptoOperationsService service, ICurrentUserAccessor currentUserAccessor, CancellationToken cancellationToken) =>
{
    var result = await service.ExecuteAsync(request, currentUserAccessor.GetCurrentUser(), cancellationToken);
    return result.IsSuccess
        ? Results.Ok(result.Value)
        : Results.BadRequest(result.Error);
});

app.MapGet("/health", () => Results.Ok(new { service = "crypto", status = "ok" }));

app.MapGet("/openapi/v1.json", () =>
{
    var document = OpenApiDocumentBuilder.Build(
        "ColdWarHistory Crypto API",
        "v1",
        [
            new OpenApiEndpointDescription("GET", "/api/crypto/catalog", "List supported ciphers and their parameters", "Crypto", false, [], [], [
                new OpenApiResponseDescription(200, "Cipher catalog")
            ]),
            new OpenApiEndpointDescription("POST", "/api/crypto/transform", "Encrypt or decrypt a message", "Crypto", false, [], [
                new OpenApiField("cipherCode", "string", true, "Cipher code"),
                new OpenApiField("mode", "string", true, "encrypt or decrypt"),
                new OpenApiField("input", "string", true, "Source message")
            ], [
                new OpenApiResponseDescription(200, "Transformation result"),
                new OpenApiResponseDescription(400, "Validation error")
            ])
        ]);

    return Results.Json(document, contentType: "application/json");
});

app.Run();
