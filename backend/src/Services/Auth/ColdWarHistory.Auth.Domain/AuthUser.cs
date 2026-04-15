using ColdWarHistory.BuildingBlocks.Domain;

namespace ColdWarHistory.Auth.Domain;

public sealed class AuthUser : AggregateRoot<Guid>
{
    private readonly List<string> _roles = [];
    private readonly List<RefreshSession> _refreshSessions = [];
    private readonly List<AccessSession> _accessSessions = [];

    public AuthUser(Guid id, string userName, string email, string passwordHash, IEnumerable<string> roles)
        : base(id)
    {
        UserName = userName;
        Email = email;
        PasswordHash = passwordHash;
        _roles.AddRange(roles);
    }

    public string UserName { get; private set; }

    public string Email { get; private set; }

    public string PasswordHash { get; private set; }

    public IReadOnlyCollection<string> Roles => _roles;

    public IReadOnlyCollection<RefreshSession> RefreshSessions => _refreshSessions;

    public IReadOnlyCollection<AccessSession> AccessSessions => _accessSessions;

    public void RegisterAccessToken(string token, DateTimeOffset expiresAt)
    {
        _accessSessions.RemoveAll(session => session.ExpiresAt <= DateTimeOffset.UtcNow);
        _accessSessions.Add(new AccessSession(token, expiresAt));
    }

    public void RegisterRefreshToken(string token, DateTimeOffset expiresAt)
    {
        _refreshSessions.RemoveAll(session => session.ExpiresAt <= DateTimeOffset.UtcNow);
        _refreshSessions.Add(new RefreshSession(token, expiresAt));
    }

    public bool HasValidPassword(string passwordHash) => PasswordHash == passwordHash;

    public bool HasActiveRefreshToken(string token, DateTimeOffset now) =>
        _refreshSessions.Any(session => session.Token == token && session.ExpiresAt > now);

    public bool HasActiveAccessToken(string token, DateTimeOffset now) =>
        _accessSessions.Any(session => session.Token == token && session.ExpiresAt > now);

    public void RevokeRefreshToken(string token) =>
        _refreshSessions.RemoveAll(session => session.Token == token);
}

public sealed record RefreshSession(string Token, DateTimeOffset ExpiresAt);

public sealed record AccessSession(string Token, DateTimeOffset ExpiresAt);
