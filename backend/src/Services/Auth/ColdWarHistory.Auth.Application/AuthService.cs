using ColdWarHistory.Auth.Domain;
using ColdWarHistory.BuildingBlocks.Application;
using ColdWarHistory.BuildingBlocks.Contracts;

namespace ColdWarHistory.Auth.Application;

public sealed class AuthService(
    IAuthUserRepository repository,
    IPasswordHasher passwordHasher,
    ITokenFactory tokenFactory,
    IClock clock) : IAuthService
{
    public async Task<OperationResult<AuthTokensResponse>> RegisterAsync(RegisterRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.UserName) || string.IsNullOrWhiteSpace(request.Email) || string.IsNullOrWhiteSpace(request.Password))
        {
            return OperationResult<AuthTokensResponse>.Failure(OperationError.Validation("UserName, email and password are required."));
        }

        if (await repository.ExistsAsync(request.UserName, request.Email, cancellationToken))
        {
            return OperationResult<AuthTokensResponse>.Failure(OperationError.Conflict("User with the same name or email already exists."));
        }

        var user = new AuthUser(
            Guid.NewGuid(),
            request.UserName.Trim(),
            request.Email.Trim(),
            passwordHasher.Hash(request.Password),
            [Roles.User]);

        var tokens = IssueTokens(user);
        await repository.AddAsync(user, cancellationToken);
        await repository.SaveChangesAsync(cancellationToken);
        return OperationResult<AuthTokensResponse>.Success(tokens);
    }

    public async Task<OperationResult<AuthTokensResponse>> LoginAsync(LoginRequest request, CancellationToken cancellationToken)
    {
        var user = await repository.FindByUserNameOrEmailAsync(request.UserNameOrEmail.Trim(), cancellationToken);
        if (user is null || !user.HasValidPassword(passwordHasher.Hash(request.Password)))
        {
            return OperationResult<AuthTokensResponse>.Failure(OperationError.Unauthorized("Invalid credentials."));
        }

        var tokens = IssueTokens(user);
        await repository.SaveChangesAsync(cancellationToken);
        return OperationResult<AuthTokensResponse>.Success(tokens);
    }

    public async Task<OperationResult<AuthTokensResponse>> RefreshAsync(RefreshTokenRequest request, CancellationToken cancellationToken)
    {
        var user = await repository.FindByRefreshTokenAsync(request.RefreshToken, cancellationToken);
        if (user is null || !user.HasActiveRefreshToken(request.RefreshToken, clock.UtcNow))
        {
            return OperationResult<AuthTokensResponse>.Failure(OperationError.Unauthorized("Refresh token is invalid or expired."));
        }

        user.RevokeRefreshToken(request.RefreshToken);
        var tokens = IssueTokens(user);
        await repository.SaveChangesAsync(cancellationToken);
        return OperationResult<AuthTokensResponse>.Success(tokens);
    }

    public async Task<OperationResult> LogoutAsync(LogoutRequest request, CancellationToken cancellationToken)
    {
        var user = await repository.FindByRefreshTokenAsync(request.RefreshToken, cancellationToken);
        if (user is null)
        {
            return OperationResult.Success();
        }

        user.RevokeRefreshToken(request.RefreshToken);
        await repository.SaveChangesAsync(cancellationToken);
        return OperationResult.Success();
    }

    public async Task<TokenIntrospectionResponse> IntrospectAsync(TokenIntrospectionRequest request, CancellationToken cancellationToken)
    {
        var user = await repository.FindByAccessTokenAsync(request.AccessToken, cancellationToken);
        if (user is null)
        {
            return new TokenIntrospectionResponse(false, null, null, Array.Empty<string>(), null);
        }

        var accessToken = user.AccessSessions
            .Where(session => session.Token == request.AccessToken && session.ExpiresAt > clock.UtcNow)
            .OrderByDescending(session => session.ExpiresAt)
            .FirstOrDefault();

        if (accessToken is null)
        {
            return new TokenIntrospectionResponse(false, null, null, Array.Empty<string>(), null);
        }

        return new TokenIntrospectionResponse(true, user.Id, user.UserName, user.Roles.ToArray(), accessToken.ExpiresAt);
    }

    private AuthTokensResponse IssueTokens(AuthUser user)
    {
        var access = tokenFactory.CreateAccessToken();
        var refresh = tokenFactory.CreateRefreshToken();
        user.RegisterAccessToken(access.Token, access.ExpiresAt);
        user.RegisterRefreshToken(refresh.Token, refresh.ExpiresAt);

        return new AuthTokensResponse(
            user.Id,
            user.UserName,
            user.Roles.ToArray(),
            access.Token,
            access.ExpiresAt,
            refresh.Token,
            refresh.ExpiresAt);
    }
}
