using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using ColdWarHistory.Auth.Application;
using ColdWarHistory.Auth.Domain;
using ColdWarHistory.BuildingBlocks.Contracts;
using Microsoft.Extensions.DependencyInjection;

namespace ColdWarHistory.Auth.Infrastructure;

public static class AuthInfrastructureExtensions
{
    public static IServiceCollection AddAuthInfrastructure(this IServiceCollection services)
    {
        services.AddSingleton<IAuthUserRepository, InMemoryAuthUserRepository>();
        services.AddSingleton<IPasswordHasher, Sha256PasswordHasher>();
        services.AddSingleton<ITokenFactory, OpaqueTokenFactory>();
        services.AddSingleton<IAuthService, AuthService>();
        return services;
    }
}

internal sealed class InMemoryAuthUserRepository : IAuthUserRepository
{
    private readonly ConcurrentDictionary<Guid, AuthUser> _users = new();

    public InMemoryAuthUserRepository(IPasswordHasher passwordHasher)
    {
        var admin = new AuthUser(
            Guid.Parse("2ecdc706-2dd5-4d49-ab0b-947b2d7d0d73"),
            "admin",
            "admin@coldwar.local",
            passwordHasher.Hash("Admin123!"),
            [Roles.Admin, Roles.Editor, Roles.User]);

        _users[admin.Id] = admin;
    }

    public Task<AuthUser?> FindByUserNameOrEmailAsync(string userNameOrEmail, CancellationToken cancellationToken)
    {
        var match = _users.Values.FirstOrDefault(user =>
            string.Equals(user.UserName, userNameOrEmail, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(user.Email, userNameOrEmail, StringComparison.OrdinalIgnoreCase));

        return Task.FromResult(match);
    }

    public Task<AuthUser?> FindByRefreshTokenAsync(string refreshToken, CancellationToken cancellationToken)
    {
        var match = _users.Values.FirstOrDefault(user => user.RefreshSessions.Any(session => session.Token == refreshToken));
        return Task.FromResult(match);
    }

    public Task<AuthUser?> FindByAccessTokenAsync(string accessToken, CancellationToken cancellationToken)
    {
        var match = _users.Values.FirstOrDefault(user => user.AccessSessions.Any(session => session.Token == accessToken));
        return Task.FromResult(match);
    }

    public Task<bool> ExistsAsync(string userName, string email, CancellationToken cancellationToken)
    {
        var exists = _users.Values.Any(user =>
            string.Equals(user.UserName, userName, StringComparison.OrdinalIgnoreCase) ||
            string.Equals(user.Email, email, StringComparison.OrdinalIgnoreCase));

        return Task.FromResult(exists);
    }

    public Task AddAsync(AuthUser user, CancellationToken cancellationToken)
    {
        _users[user.Id] = user;
        return Task.CompletedTask;
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}

internal sealed class Sha256PasswordHasher : IPasswordHasher
{
    public string Hash(string value)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(value));
        return Convert.ToHexString(bytes);
    }
}

internal sealed class OpaqueTokenFactory : ITokenFactory
{
    public (string Token, DateTimeOffset ExpiresAt) CreateAccessToken() =>
        ($"cw-access-{Guid.NewGuid():N}", DateTimeOffset.UtcNow.AddMinutes(30));

    public (string Token, DateTimeOffset ExpiresAt) CreateRefreshToken() =>
        ($"cw-refresh-{Guid.NewGuid():N}", DateTimeOffset.UtcNow.AddDays(14));
}
