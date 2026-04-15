namespace ColdWarHistory.BuildingBlocks.Contracts;

public sealed record RegisterRequest(string UserName, string Email, string Password);

public sealed record LoginRequest(string UserNameOrEmail, string Password);

public sealed record RefreshTokenRequest(string RefreshToken);

public sealed record LogoutRequest(string RefreshToken);

public sealed record AuthTokensResponse(
    Guid UserId,
    string UserName,
    IReadOnlyCollection<string> Roles,
    string AccessToken,
    DateTimeOffset AccessTokenExpiresAt,
    string RefreshToken,
    DateTimeOffset RefreshTokenExpiresAt);

public sealed record UserInfoResponse(Guid UserId, string UserName, string Email, IReadOnlyCollection<string> Roles);

public sealed record TokenIntrospectionRequest(string AccessToken);

public sealed record TokenIntrospectionResponse(
    bool IsActive,
    Guid? UserId,
    string? UserName,
    IReadOnlyCollection<string> Roles,
    DateTimeOffset? ExpiresAt);
