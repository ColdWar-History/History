using System.Net.Http.Headers;
using System.Net.Http.Json;
using ColdWarHistory.BuildingBlocks.Contracts;

namespace ColdWarHistory.Gateway.Api;

public sealed record GatewayRoute(string Prefix, string TargetBaseUrl, bool RequiresIntrospection);

public sealed class GatewayProxyService(IHttpClientFactory httpClientFactory)
{
    private static readonly IReadOnlyCollection<GatewayRoute> Routes =
    [
        new GatewayRoute(ApiRoutes.Auth, ServiceEndpoints.Auth, false),
        new GatewayRoute(ApiRoutes.Crypto, ServiceEndpoints.Crypto, true),
        new GatewayRoute(ApiRoutes.Content, ServiceEndpoints.Content, true),
        new GatewayRoute(ApiRoutes.Game, ServiceEndpoints.Game, true),
        new GatewayRoute(ApiRoutes.Progress, ServiceEndpoints.Progress, true)
    ];

    public IReadOnlyCollection<GatewayRoute> GetRoutes() => Routes;

    public GatewayRoute? Match(PathString path) =>
        Routes.FirstOrDefault(route => path.StartsWithSegments(route.Prefix, StringComparison.OrdinalIgnoreCase));

    public async Task<TokenIntrospectionResponse?> IntrospectAsync(string accessToken, CancellationToken cancellationToken)
    {
        var client = httpClientFactory.CreateClient(nameof(ServiceEndpoints.Auth));
        var response = await client.PostAsJsonAsync("/internal/auth/introspect", new TokenIntrospectionRequest(accessToken), cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        return await response.Content.ReadFromJsonAsync<TokenIntrospectionResponse>(cancellationToken);
    }

    public async Task<HttpResponseMessage> ForwardAsync(HttpContext context, GatewayRoute route, TokenIntrospectionResponse? user, CancellationToken cancellationToken)
    {
        var client = httpClientFactory.CreateClient(route.Prefix);
        var request = new HttpRequestMessage(new HttpMethod(context.Request.Method), BuildTargetUri(context, route));

        var shouldForwardBody =
            context.Request.ContentLength is > 0 ||
            context.Request.Headers.ContainsKey("Transfer-Encoding") ||
            HttpMethods.IsPost(context.Request.Method) ||
            HttpMethods.IsPut(context.Request.Method) ||
            HttpMethods.IsPatch(context.Request.Method);

        if (shouldForwardBody)
        {
            request.Content = new StreamContent(context.Request.Body);
            if (!string.IsNullOrWhiteSpace(context.Request.ContentType))
            {
                request.Content.Headers.ContentType = MediaTypeHeaderValue.Parse(context.Request.ContentType);
            }
        }

        foreach (var header in context.Request.Headers)
        {
            if (header.Key.Equals("Host", StringComparison.OrdinalIgnoreCase) ||
                header.Key.Equals("Authorization", StringComparison.OrdinalIgnoreCase) ||
                header.Key.Equals("Content-Type", StringComparison.OrdinalIgnoreCase) ||
                header.Key.Equals("Transfer-Encoding", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (!request.Headers.TryAddWithoutValidation(header.Key, header.Value.ToArray()) && request.Content is not null)
            {
                request.Content.Headers.TryAddWithoutValidation(header.Key, header.Value.ToArray());
            }
        }

        if (user?.IsActive == true && user.UserId.HasValue)
        {
            request.Headers.TryAddWithoutValidation(InternalHeaders.UserId, user.UserId.Value.ToString());
            request.Headers.TryAddWithoutValidation(InternalHeaders.UserName, user.UserName);
            request.Headers.TryAddWithoutValidation(InternalHeaders.Roles, string.Join(',', user.Roles));
            request.Headers.TryAddWithoutValidation(InternalHeaders.ForwardedByGateway, "true");
        }

        return await client.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
    }

    private static Uri BuildTargetUri(HttpContext context, GatewayRoute route)
    {
        var uri = $"{route.TargetBaseUrl}{context.Request.Path}{context.Request.QueryString}";
        return new Uri(uri);
    }
}
