namespace ColdWarHistory.BuildingBlocks.Contracts;

public static class ServiceEndpoints
{
    public const string Gateway = "http://localhost:7000";
    public const string Auth = "http://localhost:7001";
    public const string Crypto = "http://localhost:7002";
    public const string Content = "http://localhost:7003";
    public const string Game = "http://localhost:7004";
    public const string Progress = "http://localhost:7005";
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
