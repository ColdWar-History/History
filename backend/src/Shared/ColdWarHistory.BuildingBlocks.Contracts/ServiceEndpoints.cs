namespace ColdWarHistory.BuildingBlocks.Contracts;

public static class ServiceEndpoints
{
    public static string Gateway => GetEndpoint(nameof(Gateway), "http://localhost:7000");
    public static string Auth => GetEndpoint(nameof(Auth), "http://localhost:7001");
    public static string Crypto => GetEndpoint(nameof(Crypto), "http://localhost:7002");
    public static string Content => GetEndpoint(nameof(Content), "http://localhost:7003");
    public static string Game => GetEndpoint(nameof(Game), "http://localhost:7004");
    public static string Progress => GetEndpoint(nameof(Progress), "http://localhost:7005");

    private static string GetEndpoint(string serviceName, string fallback) =>
        Environment.GetEnvironmentVariable($"ServiceEndpoints__{serviceName}") ?? fallback;
}

public static class ApiRoutes
{
    public const string Auth = "/api/auth";
    public const string Crypto = "/api/crypto";
    public const string Content = "/api/content";
    public const string Game = "/api/game";
    public const string Progress = "/api/progress";
}

public static class Roles
{
    public const string Guest = "guest";
    public const string User = "user";
    public const string Editor = "editor";
    public const string Admin = "admin";
}

public static class InternalHeaders
{
    public const string UserId = "X-Internal-UserId";
    public const string UserName = "X-Internal-UserName";
    public const string Roles = "X-Internal-Roles";
    public const string ForwardedByGateway = "X-Forwarded-By-Gateway";
}
