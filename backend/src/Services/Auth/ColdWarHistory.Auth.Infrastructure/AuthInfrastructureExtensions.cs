using System.Security.Cryptography;
using System.Text;
using ColdWarHistory.Auth.Application;
using ColdWarHistory.Auth.Domain;
using ColdWarHistory.BuildingBlocks.Contracts;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace ColdWarHistory.Auth.Infrastructure;

public static class AuthInfrastructureExtensions
{
    private const string DefaultConnectionString = "Host=localhost;Port=5432;Database=coldwarhistory;Username=postgres;Password=postgres";

    public static IServiceCollection AddAuthInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton(_ => NpgsqlDataSource.Create(configuration.GetConnectionString("Default") ?? DefaultConnectionString));
        services.AddScoped<IAuthUserRepository, PostgresAuthUserRepository>();
        services.AddSingleton<IPasswordHasher, Sha256PasswordHasher>();
        services.AddSingleton<ITokenFactory, OpaqueTokenFactory>();
        services.AddScoped<IAuthService, AuthService>();
        return services;
    }
}

internal sealed class PostgresAuthUserRepository(NpgsqlDataSource dataSource) : IAuthUserRepository
{
    private readonly Dictionary<Guid, AuthUser> _tracked = [];

    public async Task<AuthUser?> FindByUserNameOrEmailAsync(string userNameOrEmail, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT u.id, u.user_name, u.email, u.password_hash
            FROM auth.users u
            WHERE lower(u.user_name) = lower(@lookup)
               OR lower(u.email) = lower(@lookup)
            LIMIT 1;
            """;

        return await LoadSingleAsync(
            sql,
            command => command.Parameters.AddWithValue("lookup", userNameOrEmail),
            cancellationToken);
    }

    public async Task<AuthUser?> FindByRefreshTokenAsync(string refreshToken, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT u.id, u.user_name, u.email, u.password_hash
            FROM auth.users u
            JOIN auth.refresh_sessions rs ON rs.user_id = u.id
            WHERE rs.token = @token
            LIMIT 1;
            """;

        return await LoadSingleAsync(
            sql,
            command => command.Parameters.AddWithValue("token", refreshToken),
            cancellationToken);
    }

    public async Task<AuthUser?> FindByAccessTokenAsync(string accessToken, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT u.id, u.user_name, u.email, u.password_hash
            FROM auth.users u
            JOIN auth.access_sessions acs ON acs.user_id = u.id
            WHERE acs.token = @token
            LIMIT 1;
            """;

        return await LoadSingleAsync(
            sql,
            command => command.Parameters.AddWithValue("token", accessToken),
            cancellationToken);
    }

    public async Task<bool> ExistsAsync(string userName, string email, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT 1
            FROM auth.users u
            WHERE lower(u.user_name) = lower(@user_name)
               OR lower(u.email) = lower(@email)
            LIMIT 1;
            """;

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("user_name", userName);
        command.Parameters.AddWithValue("email", email);
        var result = await command.ExecuteScalarAsync(cancellationToken);
        return result is not null;
    }

    public Task AddAsync(AuthUser user, CancellationToken cancellationToken)
    {
        _tracked[user.Id] = user;
        return Task.CompletedTask;
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        if (_tracked.Count == 0)
        {
            return;
        }

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        foreach (var user in _tracked.Values)
        {
            const string upsertUserSql = """
                INSERT INTO auth.users (id, user_name, email, password_hash)
                VALUES (@id, @user_name, @email, @password_hash)
                ON CONFLICT (id) DO UPDATE
                SET user_name = EXCLUDED.user_name,
                    email = EXCLUDED.email,
                    password_hash = EXCLUDED.password_hash;
                """;

            await using (var upsertUser = new NpgsqlCommand(upsertUserSql, connection, transaction))
            {
                upsertUser.Parameters.AddWithValue("id", user.Id);
                upsertUser.Parameters.AddWithValue("user_name", user.UserName);
                upsertUser.Parameters.AddWithValue("email", user.Email);
                upsertUser.Parameters.AddWithValue("password_hash", user.PasswordHash);
                await upsertUser.ExecuteNonQueryAsync(cancellationToken);
            }

            await ReplaceRolesAsync(connection, transaction, user, cancellationToken);
            await ReplaceRefreshSessionsAsync(connection, transaction, user, cancellationToken);
            await ReplaceAccessSessionsAsync(connection, transaction, user, cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
        _tracked.Clear();
    }

    private async Task<AuthUser?> LoadSingleAsync(string sql, Action<NpgsqlCommand> configure, CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);
        configure(command);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        var userId = reader.GetGuid(reader.GetOrdinal("id"));
        var userName = reader.GetString(reader.GetOrdinal("user_name"));
        var email = reader.GetString(reader.GetOrdinal("email"));
        var passwordHash = reader.GetString(reader.GetOrdinal("password_hash"));
        await reader.CloseAsync();

        var roles = await GetRolesAsync(connection, userId, cancellationToken);
        var refreshSessions = await GetRefreshSessionsAsync(connection, userId, cancellationToken);
        var accessSessions = await GetAccessSessionsAsync(connection, userId, cancellationToken);

        var user = new AuthUser(userId, userName, email, passwordHash, roles);
        foreach (var session in refreshSessions)
        {
            user.RegisterRefreshToken(session.Token, session.ExpiresAt);
        }

        foreach (var session in accessSessions)
        {
            user.RegisterAccessToken(session.Token, session.ExpiresAt);
        }

        _tracked[user.Id] = user;
        return user;
    }

    private static async Task<IReadOnlyCollection<string>> GetRolesAsync(NpgsqlConnection connection, Guid userId, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT role
            FROM auth.user_roles
            WHERE user_id = @user_id;
            """;

        var items = new List<string>();
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("user_id", userId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            items.Add(reader.GetString(0));
        }

        return items;
    }

    private static async Task<IReadOnlyCollection<RefreshSession>> GetRefreshSessionsAsync(NpgsqlConnection connection, Guid userId, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT token, expires_at
            FROM auth.refresh_sessions
            WHERE user_id = @user_id;
            """;

        var items = new List<RefreshSession>();
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("user_id", userId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            items.Add(new RefreshSession(reader.GetString(0), reader.GetFieldValue<DateTimeOffset>(1)));
        }

        return items;
    }

    private static async Task<IReadOnlyCollection<AccessSession>> GetAccessSessionsAsync(NpgsqlConnection connection, Guid userId, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT token, expires_at
            FROM auth.access_sessions
            WHERE user_id = @user_id;
            """;

        var items = new List<AccessSession>();
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("user_id", userId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            items.Add(new AccessSession(reader.GetString(0), reader.GetFieldValue<DateTimeOffset>(1)));
        }

        return items;
    }

    private static async Task ReplaceRolesAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, AuthUser user, CancellationToken cancellationToken)
    {
        const string deleteSql = "DELETE FROM auth.user_roles WHERE user_id = @user_id;";
        await using (var deleteCommand = new NpgsqlCommand(deleteSql, connection, transaction))
        {
            deleteCommand.Parameters.AddWithValue("user_id", user.Id);
            await deleteCommand.ExecuteNonQueryAsync(cancellationToken);
        }

        const string insertSql = "INSERT INTO auth.user_roles (user_id, role) VALUES (@user_id, @role);";
        foreach (var role in user.Roles)
        {
            await using var insertCommand = new NpgsqlCommand(insertSql, connection, transaction);
            insertCommand.Parameters.AddWithValue("user_id", user.Id);
            insertCommand.Parameters.AddWithValue("role", role);
            await insertCommand.ExecuteNonQueryAsync(cancellationToken);
        }
    }

    private static async Task ReplaceRefreshSessionsAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, AuthUser user, CancellationToken cancellationToken)
    {
        const string deleteSql = "DELETE FROM auth.refresh_sessions WHERE user_id = @user_id;";
        await using (var deleteCommand = new NpgsqlCommand(deleteSql, connection, transaction))
        {
            deleteCommand.Parameters.AddWithValue("user_id", user.Id);
            await deleteCommand.ExecuteNonQueryAsync(cancellationToken);
        }

        const string insertSql = "INSERT INTO auth.refresh_sessions (user_id, token, expires_at) VALUES (@user_id, @token, @expires_at);";
        foreach (var session in user.RefreshSessions)
        {
            await using var insertCommand = new NpgsqlCommand(insertSql, connection, transaction);
            insertCommand.Parameters.AddWithValue("user_id", user.Id);
            insertCommand.Parameters.AddWithValue("token", session.Token);
            insertCommand.Parameters.AddWithValue("expires_at", session.ExpiresAt);
            await insertCommand.ExecuteNonQueryAsync(cancellationToken);
        }
    }

    private static async Task ReplaceAccessSessionsAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, AuthUser user, CancellationToken cancellationToken)
    {
        const string deleteSql = "DELETE FROM auth.access_sessions WHERE user_id = @user_id;";
        await using (var deleteCommand = new NpgsqlCommand(deleteSql, connection, transaction))
        {
            deleteCommand.Parameters.AddWithValue("user_id", user.Id);
            await deleteCommand.ExecuteNonQueryAsync(cancellationToken);
        }

        const string insertSql = "INSERT INTO auth.access_sessions (user_id, token, expires_at) VALUES (@user_id, @token, @expires_at);";
        foreach (var session in user.AccessSessions)
        {
            await using var insertCommand = new NpgsqlCommand(insertSql, connection, transaction);
            insertCommand.Parameters.AddWithValue("user_id", user.Id);
            insertCommand.Parameters.AddWithValue("token", session.Token);
            insertCommand.Parameters.AddWithValue("expires_at", session.ExpiresAt);
            await insertCommand.ExecuteNonQueryAsync(cancellationToken);
        }
    }
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
