using System.Net.Http.Json;
using ColdWarHistory.BuildingBlocks.Application;
using ColdWarHistory.BuildingBlocks.Contracts;
using ColdWarHistory.Game.Application;
using ColdWarHistory.Game.Domain;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Npgsql;

namespace ColdWarHistory.Game.Infrastructure;

public static class GameInfrastructureExtensions
{
    private const string DefaultConnectionString = "Host=localhost;Port=5432;Database=coldwarhistory;Username=postgres;Password=postgres";

    public static IServiceCollection AddGameInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton(_ => NpgsqlDataSource.Create(configuration.GetConnectionString("Default") ?? DefaultConnectionString));
        services.AddScoped<IGameRepository, PostgresGameRepository>();
        services.AddHttpClient<ICryptoGameClient, CryptoGameClient>(client => client.BaseAddress = new Uri(ServiceEndpoints.Crypto));
        services.AddHttpClient<IGameProgressPublisher, GameProgressPublisher>(client => client.BaseAddress = new Uri(ServiceEndpoints.Progress));
        services.AddScoped<IGameService, GameService>();
        services.AddHostedService<DailyChallengeRefreshWorker>();
        return services;
    }
}

internal sealed class PostgresGameRepository(NpgsqlDataSource dataSource) : IGameRepository
{
    public async Task StoreChallengeAsync(TrainingChallenge challenge, CancellationToken cancellationToken)
    {
        const string sql = """
            INSERT INTO game.training_challenges (id, cipher_code, difficulty, prompt, input, expected_mode, parameters_json, expected_answer, base_score, generated_at)
            VALUES (@id, @cipher_code, @difficulty, @prompt, @input, @expected_mode, @parameters_json::jsonb, @expected_answer, @base_score, @generated_at)
            ON CONFLICT (id) DO UPDATE
            SET cipher_code = EXCLUDED.cipher_code,
                difficulty = EXCLUDED.difficulty,
                prompt = EXCLUDED.prompt,
                input = EXCLUDED.input,
                expected_mode = EXCLUDED.expected_mode,
                parameters_json = EXCLUDED.parameters_json,
                expected_answer = EXCLUDED.expected_answer,
                base_score = EXCLUDED.base_score,
                generated_at = EXCLUDED.generated_at;
            """;

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("id", challenge.Id);
        command.Parameters.AddWithValue("cipher_code", challenge.CipherCode);
        command.Parameters.AddWithValue("difficulty", challenge.Difficulty);
        command.Parameters.AddWithValue("prompt", challenge.Prompt);
        command.Parameters.AddWithValue("input", challenge.Input);
        command.Parameters.AddWithValue("expected_mode", challenge.ExpectedMode);
        command.Parameters.AddWithValue("parameters_json", SerializeParameters(challenge.Parameters));
        command.Parameters.AddWithValue("expected_answer", challenge.ExpectedAnswer);
        command.Parameters.AddWithValue("base_score", challenge.BaseScore);
        command.Parameters.AddWithValue("generated_at", challenge.GeneratedAt);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<TrainingChallenge?> GetChallengeAsync(Guid challengeId, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT id, cipher_code, difficulty, prompt, input, expected_mode, parameters_json::text, expected_answer, base_score, generated_at
            FROM game.training_challenges
            WHERE id = @id
            LIMIT 1;
            """;

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("id", challengeId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        return new TrainingChallenge(
            reader.GetGuid(0),
            reader.GetString(1),
            reader.GetString(2),
            reader.GetString(3),
            reader.GetString(4),
            reader.GetString(5),
            DeserializeParameters(reader.GetString(6)),
            reader.GetString(7),
            reader.GetInt32(8),
            reader.GetFieldValue<DateTimeOffset>(9));
    }

    public async Task StoreShiftAsync(ShiftSession session, CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        const string upsertShiftSql = """
            INSERT INTO game.shifts (id, difficulty, started_at)
            VALUES (@id, @difficulty, @started_at)
            ON CONFLICT (id) DO UPDATE
            SET difficulty = EXCLUDED.difficulty,
                started_at = EXCLUDED.started_at;
            """;

        await using (var upsertShift = new NpgsqlCommand(upsertShiftSql, connection, transaction))
        {
            upsertShift.Parameters.AddWithValue("id", session.Id);
            upsertShift.Parameters.AddWithValue("difficulty", session.Difficulty);
            upsertShift.Parameters.AddWithValue("started_at", session.StartedAt);
            await upsertShift.ExecuteNonQueryAsync(cancellationToken);
        }

        const string deleteMessagesSql = "DELETE FROM game.shift_messages WHERE shift_id = @shift_id;";
        await using (var deleteMessages = new NpgsqlCommand(deleteMessagesSql, connection, transaction))
        {
            deleteMessages.Parameters.AddWithValue("shift_id", session.Id);
            await deleteMessages.ExecuteNonQueryAsync(cancellationToken);
        }

        const string insertMessageSql = """
            INSERT INTO game.shift_messages (shift_id, message_id, headline, encoded_message, cipher_code, briefing, expected_decision)
            VALUES (@shift_id, @message_id, @headline, @encoded_message, @cipher_code, @briefing, @expected_decision);
            """;

        foreach (var message in session.Messages)
        {
            await using var insertMessage = new NpgsqlCommand(insertMessageSql, connection, transaction);
            insertMessage.Parameters.AddWithValue("shift_id", session.Id);
            insertMessage.Parameters.AddWithValue("message_id", message.Id);
            insertMessage.Parameters.AddWithValue("headline", message.Headline);
            insertMessage.Parameters.AddWithValue("encoded_message", message.EncodedMessage);
            insertMessage.Parameters.AddWithValue("cipher_code", message.CipherCode);
            insertMessage.Parameters.AddWithValue("briefing", message.Briefing);
            insertMessage.Parameters.AddWithValue("expected_decision", message.ExpectedDecision);
            await insertMessage.ExecuteNonQueryAsync(cancellationToken);
        }

        const string deleteResolutionsSql = "DELETE FROM game.shift_resolutions WHERE shift_id = @shift_id;";
        await using (var deleteResolutions = new NpgsqlCommand(deleteResolutionsSql, connection, transaction))
        {
            deleteResolutions.Parameters.AddWithValue("shift_id", session.Id);
            await deleteResolutions.ExecuteNonQueryAsync(cancellationToken);
        }

        const string insertResolutionSql = """
            INSERT INTO game.shift_resolutions (shift_id, message_id, decision, is_correct, score_delta, explanation)
            VALUES (@shift_id, @message_id, @decision, @is_correct, @score_delta, @explanation);
            """;

        foreach (var resolution in session.Resolutions.Values)
        {
            await using var insertResolution = new NpgsqlCommand(insertResolutionSql, connection, transaction);
            insertResolution.Parameters.AddWithValue("shift_id", session.Id);
            insertResolution.Parameters.AddWithValue("message_id", resolution.MessageId);
            insertResolution.Parameters.AddWithValue("decision", resolution.Decision);
            insertResolution.Parameters.AddWithValue("is_correct", resolution.IsCorrect);
            insertResolution.Parameters.AddWithValue("score_delta", resolution.ScoreDelta);
            insertResolution.Parameters.AddWithValue("explanation", resolution.Explanation);
            await insertResolution.ExecuteNonQueryAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
    }

    public async Task<ShiftSession?> GetShiftAsync(Guid shiftId, CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);

        const string shiftSql = """
            SELECT id, difficulty, started_at
            FROM game.shifts
            WHERE id = @id
            LIMIT 1;
            """;

        Guid id;
        string difficulty;
        DateTimeOffset startedAt;

        await using (var shiftCommand = new NpgsqlCommand(shiftSql, connection))
        {
            shiftCommand.Parameters.AddWithValue("id", shiftId);
            await using var shiftReader = await shiftCommand.ExecuteReaderAsync(cancellationToken);
            if (!await shiftReader.ReadAsync(cancellationToken))
            {
                return null;
            }

            id = shiftReader.GetGuid(0);
            difficulty = shiftReader.GetString(1);
            startedAt = shiftReader.GetFieldValue<DateTimeOffset>(2);
        }

        var messages = await ReadMessagesAsync(connection, id, cancellationToken);
        var session = new ShiftSession(id, difficulty, messages, startedAt);

        var resolutions = await ReadResolutionsAsync(connection, id, cancellationToken);
        foreach (var resolution in resolutions)
        {
            session.RecordResolution(resolution);
        }

        return session;
    }

    public async Task<DailyChallengeSnapshot?> GetDailyChallengeAsync(CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT challenge_date, challenge_id, theme
            FROM game.daily_challenge
            ORDER BY challenge_date DESC
            LIMIT 1;
            """;

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        var date = reader.GetFieldValue<DateOnly>(0);
        var challengeId = reader.GetGuid(1);
        var theme = reader.GetString(2);

        await reader.CloseAsync();
        var challenge = await GetChallengeAsync(challengeId, cancellationToken);
        if (challenge is null)
        {
            return null;
        }

        return new DailyChallengeSnapshot(date, challenge, theme);
    }

    public async Task SetDailyChallengeAsync(DailyChallengeSnapshot snapshot, CancellationToken cancellationToken)
    {
        const string sql = """
            INSERT INTO game.daily_challenge (challenge_date, challenge_id, theme)
            VALUES (@challenge_date, @challenge_id, @theme)
            ON CONFLICT (challenge_date) DO UPDATE
            SET challenge_id = EXCLUDED.challenge_id,
                theme = EXCLUDED.theme;
            """;

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("challenge_date", snapshot.Date);
        command.Parameters.AddWithValue("challenge_id", snapshot.Challenge.Id);
        command.Parameters.AddWithValue("theme", snapshot.Theme);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static string SerializeParameters(IReadOnlyDictionary<string, string> parameters) =>
        System.Text.Json.JsonSerializer.Serialize(parameters);

    private static IReadOnlyDictionary<string, string> DeserializeParameters(string json) =>
        System.Text.Json.JsonSerializer.Deserialize<Dictionary<string, string>>(json)
        ?? new Dictionary<string, string>();

    private static async Task<IReadOnlyCollection<ShiftMessage>> ReadMessagesAsync(NpgsqlConnection connection, Guid shiftId, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT message_id, headline, encoded_message, cipher_code, briefing, expected_decision
            FROM game.shift_messages
            WHERE shift_id = @shift_id;
            """;

        var items = new List<ShiftMessage>();
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("shift_id", shiftId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            items.Add(new ShiftMessage(
                reader.GetGuid(0),
                reader.GetString(1),
                reader.GetString(2),
                reader.GetString(3),
                reader.GetString(4),
                reader.GetString(5)));
        }

        return items;
    }

    private static async Task<IReadOnlyCollection<ShiftResolution>> ReadResolutionsAsync(NpgsqlConnection connection, Guid shiftId, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT message_id, decision, is_correct, score_delta, explanation
            FROM game.shift_resolutions
            WHERE shift_id = @shift_id;
            """;

        var items = new List<ShiftResolution>();
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("shift_id", shiftId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            items.Add(new ShiftResolution(
                reader.GetGuid(0),
                reader.GetString(1),
                reader.GetBoolean(2),
                reader.GetInt32(3),
                reader.GetString(4)));
        }

        return items;
    }
}

internal sealed class CryptoGameClient(HttpClient httpClient) : ICryptoGameClient
{
    public async Task<string> ExecuteAsync(string cipherCode, string mode, string input, IReadOnlyDictionary<string, string> parameters, CancellationToken cancellationToken)
    {
        var response = await httpClient.PostAsJsonAsync(
            "/api/crypto/transform",
            new CryptoTransformRequest(cipherCode, mode, input, new Dictionary<string, string>(parameters)),
            cancellationToken);

        response.EnsureSuccessStatusCode();
        var payload = await response.Content.ReadFromJsonAsync<CryptoTransformResponse>(cancellationToken);
        return payload?.Output ?? throw new InvalidOperationException("Crypto service returned empty response.");
    }
}

internal sealed class GameProgressPublisher(HttpClient httpClient) : IGameProgressPublisher
{
    public async Task PublishChallengeCompletedAsync(ChallengeCompletedEvent integrationEvent, CancellationToken cancellationToken)
    {
        await httpClient.PostAsJsonAsync("/internal/events/challenge-completed", integrationEvent, cancellationToken);
    }

    public async Task PublishShiftCompletedAsync(ShiftCompletedEvent integrationEvent, CancellationToken cancellationToken)
    {
        await httpClient.PostAsJsonAsync("/internal/events/shift-completed", integrationEvent, cancellationToken);
    }
}

internal sealed class DailyChallengeRefreshWorker(IServiceProvider serviceProvider) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(1));

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = serviceProvider.CreateScope();
                var service = scope.ServiceProvider.GetRequiredService<IGameService>();
                await service.RefreshDailyChallengeAsync(stoppingToken);
            }
            catch
            {
                // Ignore transient startup failures; the next tick will retry.
            }

            if (!await timer.WaitForNextTickAsync(stoppingToken))
            {
                break;
            }
        }
    }
}
