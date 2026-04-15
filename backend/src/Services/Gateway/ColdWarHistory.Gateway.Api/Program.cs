using System.Text.Json.Nodes;
using ColdWarHistory.BuildingBlocks.Api;
using ColdWarHistory.BuildingBlocks.Contracts;
using ColdWarHistory.Gateway.Api;

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.UseUrls(ServiceEndpoints.Gateway);

builder.Services.AddColdWarApiDefaults();
builder.Services.AddHttpClient(nameof(ServiceEndpoints.Auth), client => client.BaseAddress = new Uri(ServiceEndpoints.Auth));
builder.Services.AddHttpClient(ApiRoutes.Auth, client => client.BaseAddress = new Uri(ServiceEndpoints.Auth));
builder.Services.AddHttpClient(ApiRoutes.Crypto, client => client.BaseAddress = new Uri(ServiceEndpoints.Crypto));
builder.Services.AddHttpClient(ApiRoutes.Content, client => client.BaseAddress = new Uri(ServiceEndpoints.Content));
builder.Services.AddHttpClient(ApiRoutes.Game, client => client.BaseAddress = new Uri(ServiceEndpoints.Game));
builder.Services.AddHttpClient(ApiRoutes.Progress, client => client.BaseAddress = new Uri(ServiceEndpoints.Progress));
builder.Services.AddSingleton<GatewayProxyService>();

var app = builder.Build();

app.UseRequestAudit("gateway");

app.MapGet("/health", async (GatewayProxyService proxyService, CancellationToken cancellationToken) =>
{
    var clientFactory = app.Services.GetRequiredService<IHttpClientFactory>();
    var checks = new List<object>();

    foreach (var route in proxyService.GetRoutes())
    {
        try
        {
            var client = clientFactory.CreateClient(route.Prefix);
            var response = await client.GetAsync("/health", cancellationToken);
            checks.Add(new { route = route.Prefix, status = response.IsSuccessStatusCode ? "ok" : "degraded" });
        }
        catch
        {
            checks.Add(new { route = route.Prefix, status = "down" });
        }
    }

    return Results.Ok(new { gateway = "ok", services = checks });
});

app.MapGet("/openapi/v1.json", () =>
{
    var document = OpenApiDocumentBuilder.Build(
        "ColdWarHistory Gateway API",
        "v1",
        [
            new OpenApiEndpointDescription("POST", "/api/auth/login", "Authenticate user", "Gateway", false, [], [
                new OpenApiField("userNameOrEmail", "string", true, "Credentials login"),
                new OpenApiField("password", "string", true, "Credentials password")
            ], [new OpenApiResponseDescription(200, "Tokens issued")]),
            new OpenApiEndpointDescription("POST", "/api/crypto/transform", "Run crypto operation through gateway", "Gateway", false, [], [
                new OpenApiField("cipherCode", "string", true, "Cipher code"),
                new OpenApiField("mode", "string", true, "encrypt or decrypt"),
                new OpenApiField("input", "string", true, "Source message")
            ], [new OpenApiResponseDescription(200, "Operation completed")]),
            new OpenApiEndpointDescription("GET", "/api/content/ciphers", "Read content catalog through gateway", "Gateway", false, [], [], [new OpenApiResponseDescription(200, "Catalog")]),
            new OpenApiEndpointDescription("GET", "/api/game/daily", "Read daily challenge through gateway", "Gateway", false, [], [], [new OpenApiResponseDescription(200, "Daily challenge")]),
            new OpenApiEndpointDescription("GET", "/api/progress/profile", "Read current user profile through gateway", "Gateway", true, [Roles.User, Roles.Editor, Roles.Admin], [], [new OpenApiResponseDescription(200, "Profile")])
        ]);

    return Results.Json(document, contentType: "application/json");
});

app.Map("/{**catchAll}", async (HttpContext context, GatewayProxyService proxyService, CancellationToken cancellationToken) =>
{
    var route = proxyService.Match(context.Request.Path);
    if (route is null)
    {
        context.Response.StatusCode = StatusCodes.Status404NotFound;
        await context.Response.WriteAsJsonAsync(new { error = "Route not found." }, cancellationToken);
        return;
    }

    TokenIntrospectionResponse? introspection = null;
    var token = context.Request.Headers.Authorization.ToString();
    if (!string.IsNullOrWhiteSpace(token) && token.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
    {
        introspection = await proxyService.IntrospectAsync(token["Bearer ".Length..].Trim(), cancellationToken);
        if (introspection is null || !introspection.IsActive)
        {
            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
            await context.Response.WriteAsJsonAsync(new { error = "Access token is invalid or expired." }, cancellationToken);
            return;
        }
    }

    var response = await proxyService.ForwardAsync(context, route, introspection, cancellationToken);
    context.Response.StatusCode = (int)response.StatusCode;

    foreach (var header in response.Headers)
    {
        context.Response.Headers[header.Key] = header.Value.ToArray();
    }

    foreach (var header in response.Content.Headers)
    {
        context.Response.Headers[header.Key] = header.Value.ToArray();
    }

    context.Response.Headers.Remove("transfer-encoding");
    await response.Content.CopyToAsync(context.Response.Body, cancellationToken);
});

app.Run();
