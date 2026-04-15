using ColdWarHistory.Auth.Domain;
using ColdWarHistory.BuildingBlocks.Application;
using ColdWarHistory.BuildingBlocks.Contracts;

namespace ColdWarHistory.Auth.Application;

public interface IAuthUserRepository
{
    Task<AuthUser?> FindByUserNameOrEmailAsync(string userNameOrEmail, CancellationToken cancellationToken);
    Task<AuthUser?> FindByRefreshTokenAsync(string refreshToken, CancellationToken cancellationToken);
    Task<AuthUser?> FindByAccessTokenAsync(string accessToken, CancellationToken cancellationToken);
    Task<bool> ExistsAsync(string userName, string email, CancellationToken cancellationToken);
    Task AddAsync(AuthUser user, CancellationToken cancellationToken);
    Task SaveChangesAsync(CancellationToken cancellationToken);
}

public interface IPasswordHasher
{
    string Hash(string value);
}

public interface ITokenFactory
{
    (string Token, DateTimeOffset ExpiresAt) CreateAccessToken();
    (string Token, DateTimeOffset ExpiresAt) CreateRefreshToken();
}

public interface IAuthService
{
    Task<OperationResult<AuthTokensResponse>> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken);
    Task<OperationResult<AuthTokensResponse>> LoginAsync(LoginRequest request, CancellationToken cancellationToken);
    Task<OperationResult<AuthTokensResponse>> RefreshAsync(RefreshTokenRequest request, CancellationToken cancellationToken);
    Task<OperationResult> LogoutAsync(LogoutRequest request, CancellationToken cancellationToken);
    Task<TokenIntrospectionResponse> IntrospectAsync(TokenIntrospectionRequest request, CancellationToken cancellationToken);
}
