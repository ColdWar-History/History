using ColdWarHistory.Progress.Application;
using ColdWarHistory.Progress.Domain;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace ColdWarHistory.Progress.Infrastructure;

public static class ProgressInfrastructureExtensions
{
    private const string DefaultConnectionString = "Host=localhost;Port=5432;Database=coldwarhistory;Username=postgres;Password=postgres";

    public static IServiceCollection AddProgressInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton(_ => NpgsqlDataSource.Create(configuration.GetConnectionString("Default") ?? DefaultConnectionString));
        services.AddScoped<IProgressRepository, PostgresProgressRepository>();
        services.AddScoped<IProgressService, ProgressService>();
        return services;
    }
}

internal sealed class PostgresProgressRepository(NpgsqlDataSource dataSource) : IProgressRepository
{
    private readonly Dictionary<Guid, UserProgressAggregate> _tracked = [];

    public async Task<UserProgressAggregate> GetOrCreateAsync(Guid userId, string userName, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT user_id, user_name, total_score, challenges_completed, correct_challenges, shift_reports_completed
            FROM progress.user_progress
            WHERE user_id = @user_id
            LIMIT 1;
            """;

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("user_id", userId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        var hasRow = await reader.ReadAsync(cancellationToken);
        Guid persistedUserId = Guid.Empty;
        string persistedUserName = string.Empty;
        var totalScore = 0;
        var challengesCompleted = 0;
        var correctChallenges = 0;
        var shiftReportsCompleted = 0;

        if (hasRow)
        {
            persistedUserId = reader.GetGuid(0);
            persistedUserName = reader.GetString(1);
            totalScore = reader.GetInt32(2);
            challengesCompleted = reader.GetInt32(3);
            correctChallenges = reader.GetInt32(4);
            shiftReportsCompleted = reader.GetInt32(5);
        }

        await reader.CloseAsync();

        UserProgressAggregate aggregate;
        if (hasRow)
        {
            aggregate = new UserProgressAggregate(
                persistedUserId,
                persistedUserName,
                totalScore,
                challengesCompleted,
                correctChallenges,
                shiftReportsCompleted,
                await ReadOperationsAsync(connection, userId, cancellationToken),
                await ReadAchievementsAsync(connection, userId, cancellationToken));
        }
        else
        {
            aggregate = new UserProgressAggregate(userId, userName);
        }

        aggregate.Rename(userName);
        _tracked[userId] = aggregate;
        return aggregate;
    }

    public async Task<IReadOnlyCollection<UserProgressAggregate>> GetAllAsync(CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT user_id, user_name, total_score, challenges_completed, correct_challenges, shift_reports_completed
            FROM progress.user_progress;
            """;

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        var rows = new List<(Guid UserId, string UserName, int TotalScore, int ChallengesCompleted, int CorrectChallenges, int ShiftReportsCompleted)>();
        while (await reader.ReadAsync(cancellationToken))
        {
            rows.Add((
                reader.GetGuid(0),
                reader.GetString(1),
                reader.GetInt32(2),
                reader.GetInt32(3),
                reader.GetInt32(4),
                reader.GetInt32(5)));
        }

            await reader.CloseAsync();

        var items = new List<UserProgressAggregate>(rows.Count);
        foreach (var row in rows)
        {
            var aggregate = new UserProgressAggregate(
                row.UserId,
                row.UserName,
                row.TotalScore,
                row.ChallengesCompleted,
                row.CorrectChallenges,
                row.ShiftReportsCompleted,
                await ReadOperationsAsync(connection, row.UserId, cancellationToken),
                await ReadAchievementsAsync(connection, row.UserId, cancellationToken));

            _tracked[row.UserId] = aggregate;
            items.Add(aggregate);
        }

        return items;
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        if (_tracked.Count == 0)
        {
            return;
        }

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        foreach (var aggregate in _tracked.Values)
        {
            const string upsertUserSql = """
                INSERT INTO progress.user_progress (user_id, user_name, total_score, challenges_completed, correct_challenges, shift_reports_completed, updated_at)
                VALUES (@user_id, @user_name, @total_score, @challenges_completed, @correct_challenges, @shift_reports_completed, now())
                ON CONFLICT (user_id) DO UPDATE
                SET user_name = EXCLUDED.user_name,
                    total_score = EXCLUDED.total_score,
                    challenges_completed = EXCLUDED.challenges_completed,
                    correct_challenges = EXCLUDED.correct_challenges,
                    shift_reports_completed = EXCLUDED.shift_reports_completed,
                    updated_at = now();
                """;

            await using (var upsertUser = new NpgsqlCommand(upsertUserSql, connection, transaction))
            {
                upsertUser.Parameters.AddWithValue("user_id", aggregate.Id);
                upsertUser.Parameters.AddWithValue("user_name", aggregate.UserName);
                upsertUser.Parameters.AddWithValue("total_score", aggregate.TotalScore);
                upsertUser.Parameters.AddWithValue("challenges_completed", aggregate.ChallengesCompleted);
                upsertUser.Parameters.AddWithValue("correct_challenges", aggregate.CorrectChallenges);
                upsertUser.Parameters.AddWithValue("shift_reports_completed", aggregate.ShiftReportsCompleted);
                await upsertUser.ExecuteNonQueryAsync(cancellationToken);
            }

            await ReplaceOperationsAsync(connection, transaction, aggregate, cancellationToken);
            await ReplaceAchievementsAsync(connection, transaction, aggregate, cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
        _tracked.Clear();
    }

    private static async Task<IReadOnlyCollection<CryptoOperationEntry>> ReadOperationsAsync(NpgsqlConnection connection, Guid userId, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT operation_id, cipher_code, mode, input, output, processed_at
            FROM progress.crypto_operations
            WHERE user_id = @user_id
            ORDER BY processed_at DESC
            LIMIT 20;
            """;

        var items = new List<CryptoOperationEntry>();
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("user_id", userId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            items.Add(new CryptoOperationEntry(
                reader.GetGuid(0),
                reader.GetString(1),
                reader.GetString(2),
                reader.GetString(3),
                reader.GetString(4),
                reader.GetFieldValue<DateTimeOffset>(5)));
        }

        return items;
    }

    private static async Task<IReadOnlyCollection<AchievementEntry>> ReadAchievementsAsync(NpgsqlConnection connection, Guid userId, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT code, title, description, unlocked_at
            FROM progress.achievements
            WHERE user_id = @user_id
            ORDER BY unlocked_at;
            """;

        var items = new List<AchievementEntry>();
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("user_id", userId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            items.Add(new AchievementEntry(
                reader.GetString(0),
                reader.GetString(1),
                reader.GetString(2),
                reader.GetFieldValue<DateTimeOffset>(3)));
        }

        return items;
    }

    private static async Task ReplaceOperationsAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, UserProgressAggregate aggregate, CancellationToken cancellationToken)
    {
        const string deleteSql = "DELETE FROM progress.crypto_operations WHERE user_id = @user_id;";
        await using (var deleteCommand = new NpgsqlCommand(deleteSql, connection, transaction))
        {
            deleteCommand.Parameters.AddWithValue("user_id", aggregate.Id);
            await deleteCommand.ExecuteNonQueryAsync(cancellationToken);
        }

        const string insertSql = """
            INSERT INTO progress.crypto_operations (user_id, operation_id, cipher_code, mode, input, output, processed_at)
            VALUES (@user_id, @operation_id, @cipher_code, @mode, @input, @output, @processed_at);
            """;

        foreach (var operation in aggregate.Operations)
        {
            await using var insertCommand = new NpgsqlCommand(insertSql, connection, transaction);
            insertCommand.Parameters.AddWithValue("user_id", aggregate.Id);
            insertCommand.Parameters.AddWithValue("operation_id", operation.OperationId);
            insertCommand.Parameters.AddWithValue("cipher_code", operation.CipherCode);
            insertCommand.Parameters.AddWithValue("mode", operation.Mode);
            insertCommand.Parameters.AddWithValue("input", operation.Input);
            insertCommand.Parameters.AddWithValue("output", operation.Output);
            insertCommand.Parameters.AddWithValue("processed_at", operation.ProcessedAt);
            await insertCommand.ExecuteNonQueryAsync(cancellationToken);
        }
    }

    private static async Task ReplaceAchievementsAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, UserProgressAggregate aggregate, CancellationToken cancellationToken)
    {
        const string deleteSql = "DELETE FROM progress.achievements WHERE user_id = @user_id;";
        await using (var deleteCommand = new NpgsqlCommand(deleteSql, connection, transaction))
        {
            deleteCommand.Parameters.AddWithValue("user_id", aggregate.Id);
            await deleteCommand.ExecuteNonQueryAsync(cancellationToken);
        }

        const string insertSql = """
            INSERT INTO progress.achievements (user_id, code, title, description, unlocked_at)
            VALUES (@user_id, @code, @title, @description, @unlocked_at);
            """;

        foreach (var achievement in aggregate.Achievements)
        {
            await using var insertCommand = new NpgsqlCommand(insertSql, connection, transaction);
            insertCommand.Parameters.AddWithValue("user_id", aggregate.Id);
            insertCommand.Parameters.AddWithValue("code", achievement.Code);
            insertCommand.Parameters.AddWithValue("title", achievement.Title);
            insertCommand.Parameters.AddWithValue("description", achievement.Description);
            insertCommand.Parameters.AddWithValue("unlocked_at", achievement.UnlockedAt);
            await insertCommand.ExecuteNonQueryAsync(cancellationToken);
        }
    }
}
