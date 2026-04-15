using ColdWarHistory.Content.Application;
using ColdWarHistory.Content.Domain;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;

namespace ColdWarHistory.Content.Infrastructure;

public static class ContentInfrastructureExtensions
{
    private const string DefaultConnectionString = "Host=localhost;Port=5432;Database=coldwarhistory;Username=postgres;Password=postgres";

    public static IServiceCollection AddContentInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddSingleton(_ => NpgsqlDataSource.Create(configuration.GetConnectionString("Default") ?? DefaultConnectionString));
        services.AddScoped<IContentRepository, PostgresContentRepository>();
        services.AddScoped<IContentService, ContentService>();
        return services;
    }
}

internal sealed class PostgresContentRepository(NpgsqlDataSource dataSource) : IContentRepository
{
    private readonly Dictionary<Guid, CipherCard> _trackedCiphers = [];
    private readonly Dictionary<Guid, HistoricalEvent> _trackedEvents = [];
    private readonly Dictionary<Guid, CuratedCollection> _trackedCollections = [];

    public async Task<IReadOnlyCollection<CipherCard>> GetCiphersAsync(CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        const string sql = """
            SELECT id, code, name, category, era, difficulty, summary, description, example, publication_status
            FROM content.ciphers;
            """;

        var rows = new List<CipherRow>();
        await using var command = new NpgsqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            rows.Add(new CipherRow(
                reader.GetGuid(reader.GetOrdinal("id")),
                reader.GetString(reader.GetOrdinal("code")),
                reader.GetString(reader.GetOrdinal("name")),
                reader.GetString(reader.GetOrdinal("category")),
                reader.GetString(reader.GetOrdinal("era")),
                reader.GetInt32(reader.GetOrdinal("difficulty")),
                reader.GetString(reader.GetOrdinal("summary")),
                reader.GetString(reader.GetOrdinal("description")),
                reader.GetString(reader.GetOrdinal("example")),
                ParseStatus(reader.GetString(reader.GetOrdinal("publication_status")))));
        }

        await reader.CloseAsync();

        var items = new List<CipherCard>(rows.Count);
        foreach (var row in rows)
        {
            var cipher = await ReadCipherAsync(connection, row, cancellationToken);
            items.Add(cipher);
            _trackedCiphers[cipher.Id] = cipher;
        }

        return items;
    }

    public async Task<CipherCard?> GetCipherAsync(Guid id, CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        const string sql = """
            SELECT id, code, name, category, era, difficulty, summary, description, example, publication_status
            FROM content.ciphers
            WHERE id = @id
            LIMIT 1;
            """;

        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("id", id);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        var row = new CipherRow(
            reader.GetGuid(reader.GetOrdinal("id")),
            reader.GetString(reader.GetOrdinal("code")),
            reader.GetString(reader.GetOrdinal("name")),
            reader.GetString(reader.GetOrdinal("category")),
            reader.GetString(reader.GetOrdinal("era")),
            reader.GetInt32(reader.GetOrdinal("difficulty")),
            reader.GetString(reader.GetOrdinal("summary")),
            reader.GetString(reader.GetOrdinal("description")),
            reader.GetString(reader.GetOrdinal("example")),
            ParseStatus(reader.GetString(reader.GetOrdinal("publication_status"))));

        await reader.CloseAsync();
        var cipher = await ReadCipherAsync(connection, row, cancellationToken);
        _trackedCiphers[cipher.Id] = cipher;
        return cipher;
    }

    public Task AddCipherAsync(CipherCard cipher, CancellationToken cancellationToken)
    {
        _trackedCiphers[cipher.Id] = cipher;
        return Task.CompletedTask;
    }

    public async Task<IReadOnlyCollection<HistoricalEvent>> GetEventsAsync(CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        const string sql = """
            SELECT id, title, event_date, region, topic, summary, description, publication_status
            FROM content.historical_events;
            """;

        var rows = new List<EventRow>();
        await using var command = new NpgsqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            rows.Add(new EventRow(
                reader.GetGuid(reader.GetOrdinal("id")),
                reader.GetString(reader.GetOrdinal("title")),
                reader.GetFieldValue<DateOnly>(reader.GetOrdinal("event_date")),
                reader.GetString(reader.GetOrdinal("region")),
                reader.GetString(reader.GetOrdinal("topic")),
                reader.GetString(reader.GetOrdinal("summary")),
                reader.GetString(reader.GetOrdinal("description")),
                ParseStatus(reader.GetString(reader.GetOrdinal("publication_status")))));
        }

        await reader.CloseAsync();

        var items = new List<HistoricalEvent>(rows.Count);
        foreach (var row in rows)
        {
            var historicalEvent = await ReadEventAsync(connection, row, cancellationToken);
            items.Add(historicalEvent);
            _trackedEvents[historicalEvent.Id] = historicalEvent;
        }

        return items;
    }

    public async Task<HistoricalEvent?> GetEventAsync(Guid id, CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        const string sql = """
            SELECT id, title, event_date, region, topic, summary, description, publication_status
            FROM content.historical_events
            WHERE id = @id
            LIMIT 1;
            """;

        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("id", id);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        var row = new EventRow(
            reader.GetGuid(reader.GetOrdinal("id")),
            reader.GetString(reader.GetOrdinal("title")),
            reader.GetFieldValue<DateOnly>(reader.GetOrdinal("event_date")),
            reader.GetString(reader.GetOrdinal("region")),
            reader.GetString(reader.GetOrdinal("topic")),
            reader.GetString(reader.GetOrdinal("summary")),
            reader.GetString(reader.GetOrdinal("description")),
            ParseStatus(reader.GetString(reader.GetOrdinal("publication_status"))));

        await reader.CloseAsync();
        var historicalEvent = await ReadEventAsync(connection, row, cancellationToken);
        _trackedEvents[historicalEvent.Id] = historicalEvent;
        return historicalEvent;
    }

    public Task AddEventAsync(HistoricalEvent historicalEvent, CancellationToken cancellationToken)
    {
        _trackedEvents[historicalEvent.Id] = historicalEvent;
        return Task.CompletedTask;
    }

    public async Task<IReadOnlyCollection<CuratedCollection>> GetCollectionsAsync(CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        const string sql = """
            SELECT id, title, theme, summary, publication_status
            FROM content.collections;
            """;

        var rows = new List<CollectionRow>();
        await using var command = new NpgsqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            rows.Add(new CollectionRow(
                reader.GetGuid(reader.GetOrdinal("id")),
                reader.GetString(reader.GetOrdinal("title")),
                reader.GetString(reader.GetOrdinal("theme")),
                reader.GetString(reader.GetOrdinal("summary")),
                ParseStatus(reader.GetString(reader.GetOrdinal("publication_status")))));
        }

        await reader.CloseAsync();

        var items = new List<CuratedCollection>(rows.Count);
        foreach (var row in rows)
        {
            var collection = await ReadCollectionAsync(connection, row, cancellationToken);
            items.Add(collection);
            _trackedCollections[collection.Id] = collection;
        }

        return items;
    }

    public async Task<CuratedCollection?> GetCollectionAsync(Guid id, CancellationToken cancellationToken)
    {
        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        const string sql = """
            SELECT id, title, theme, summary, publication_status
            FROM content.collections
            WHERE id = @id
            LIMIT 1;
            """;

        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("id", id);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        var row = new CollectionRow(
            reader.GetGuid(reader.GetOrdinal("id")),
            reader.GetString(reader.GetOrdinal("title")),
            reader.GetString(reader.GetOrdinal("theme")),
            reader.GetString(reader.GetOrdinal("summary")),
            ParseStatus(reader.GetString(reader.GetOrdinal("publication_status"))));

        await reader.CloseAsync();
        var collection = await ReadCollectionAsync(connection, row, cancellationToken);
        _trackedCollections[collection.Id] = collection;
        return collection;
    }

    public Task AddCollectionAsync(CuratedCollection collection, CancellationToken cancellationToken)
    {
        _trackedCollections[collection.Id] = collection;
        return Task.CompletedTask;
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken)
    {
        if (_trackedCiphers.Count == 0 && _trackedEvents.Count == 0 && _trackedCollections.Count == 0)
        {
            return;
        }

        await using var connection = await dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        foreach (var cipher in _trackedCiphers.Values)
        {
            await UpsertCipherAsync(connection, transaction, cipher, cancellationToken);
            await ReplaceCipherRelatedEventsAsync(connection, transaction, cipher, cancellationToken);
            await ReplaceCipherVersionsAsync(connection, transaction, cipher, cancellationToken);
        }

        foreach (var historicalEvent in _trackedEvents.Values)
        {
            await UpsertEventAsync(connection, transaction, historicalEvent, cancellationToken);
            await ReplaceEventParticipantsAsync(connection, transaction, historicalEvent, cancellationToken);
            await ReplaceEventCipherCodesAsync(connection, transaction, historicalEvent, cancellationToken);
        }

        foreach (var collection in _trackedCollections.Values)
        {
            await UpsertCollectionAsync(connection, transaction, collection, cancellationToken);
            await ReplaceCollectionEventsAsync(connection, transaction, collection, cancellationToken);
            await ReplaceCollectionCipherCodesAsync(connection, transaction, collection, cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
        _trackedCiphers.Clear();
        _trackedEvents.Clear();
        _trackedCollections.Clear();
    }

    private static async Task<CipherCard> ReadCipherAsync(NpgsqlConnection connection, CipherRow row, CancellationToken cancellationToken)
    {
        var relatedEventIds = await ReadCipherRelatedEventsAsync(connection, row.Id, cancellationToken);
        var versions = await ReadCipherVersionsAsync(connection, row.Id, cancellationToken);

        return new CipherCard(row.Id, row.Code, row.Name, row.Category, row.Era, row.Difficulty, row.Summary, row.Description, row.Example, relatedEventIds, row.Status, versions);
    }

    private static async Task<IReadOnlyCollection<Guid>> ReadCipherRelatedEventsAsync(NpgsqlConnection connection, Guid cipherId, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT event_id
            FROM content.cipher_related_events
            WHERE cipher_id = @cipher_id;
            """;

        var items = new List<Guid>();
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("cipher_id", cipherId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            items.Add(reader.GetGuid(0));
        }

        return items;
    }

    private static async Task<IReadOnlyCollection<ContentVersion>> ReadCipherVersionsAsync(NpgsqlConnection connection, Guid cipherId, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT version_number, edited_by, updated_at, change_summary
            FROM content.cipher_versions
            WHERE cipher_id = @cipher_id
            ORDER BY version_number;
            """;

        var items = new List<ContentVersion>();
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("cipher_id", cipherId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            items.Add(new ContentVersion(
                reader.GetInt32(0),
                reader.GetString(1),
                reader.GetFieldValue<DateTimeOffset>(2),
                reader.GetString(3)));
        }

        return items;
    }

    private static async Task<HistoricalEvent> ReadEventAsync(NpgsqlConnection connection, EventRow row, CancellationToken cancellationToken)
    {
        var participants = await ReadEventParticipantsAsync(connection, row.Id, cancellationToken);
        var cipherCodes = await ReadEventCipherCodesAsync(connection, row.Id, cancellationToken);

        return new HistoricalEvent(row.Id, row.Title, row.EventDate, row.Region, row.Topic, row.Summary, row.Description, participants, cipherCodes, row.Status);
    }

    private static async Task<IReadOnlyCollection<string>> ReadEventParticipantsAsync(NpgsqlConnection connection, Guid eventId, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT participant
            FROM content.event_participants
            WHERE event_id = @event_id;
            """;

        var items = new List<string>();
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("event_id", eventId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            items.Add(reader.GetString(0));
        }

        return items;
    }

    private static async Task<IReadOnlyCollection<string>> ReadEventCipherCodesAsync(NpgsqlConnection connection, Guid eventId, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT cipher_code
            FROM content.event_cipher_codes
            WHERE event_id = @event_id;
            """;

        var items = new List<string>();
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("event_id", eventId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            items.Add(reader.GetString(0));
        }

        return items;
    }

    private static async Task<CuratedCollection> ReadCollectionAsync(NpgsqlConnection connection, CollectionRow row, CancellationToken cancellationToken)
    {
        var eventIds = await ReadCollectionEventsAsync(connection, row.Id, cancellationToken);
        var cipherCodes = await ReadCollectionCipherCodesAsync(connection, row.Id, cancellationToken);

        return new CuratedCollection(row.Id, row.Title, row.Theme, row.Summary, eventIds, cipherCodes, row.Status);
    }

    private static async Task<IReadOnlyCollection<Guid>> ReadCollectionEventsAsync(NpgsqlConnection connection, Guid collectionId, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT event_id
            FROM content.collection_events
            WHERE collection_id = @collection_id;
            """;

        var items = new List<Guid>();
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("collection_id", collectionId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            items.Add(reader.GetGuid(0));
        }

        return items;
    }

    private static async Task<IReadOnlyCollection<string>> ReadCollectionCipherCodesAsync(NpgsqlConnection connection, Guid collectionId, CancellationToken cancellationToken)
    {
        const string sql = """
            SELECT cipher_code
            FROM content.collection_cipher_codes
            WHERE collection_id = @collection_id;
            """;

        var items = new List<string>();
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("collection_id", collectionId);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            items.Add(reader.GetString(0));
        }

        return items;
    }

    private static PublicationStatus ParseStatus(string value) =>
        Enum.TryParse<PublicationStatus>(value, true, out var status)
            ? status
            : PublicationStatus.Draft;

    private static async Task UpsertCipherAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, CipherCard cipher, CancellationToken cancellationToken)
    {
        const string sql = """
            INSERT INTO content.ciphers (id, code, name, category, era, difficulty, summary, description, example, publication_status, updated_at)
            VALUES (@id, @code, @name, @category, @era, @difficulty, @summary, @description, @example, @publication_status, now())
            ON CONFLICT (id) DO UPDATE
            SET code = EXCLUDED.code,
                name = EXCLUDED.name,
                category = EXCLUDED.category,
                era = EXCLUDED.era,
                difficulty = EXCLUDED.difficulty,
                summary = EXCLUDED.summary,
                description = EXCLUDED.description,
                example = EXCLUDED.example,
                publication_status = EXCLUDED.publication_status,
                updated_at = now();
            """;

        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("id", cipher.Id);
        command.Parameters.AddWithValue("code", cipher.Code);
        command.Parameters.AddWithValue("name", cipher.Name);
        command.Parameters.AddWithValue("category", cipher.Category);
        command.Parameters.AddWithValue("era", cipher.Era);
        command.Parameters.AddWithValue("difficulty", cipher.Difficulty);
        command.Parameters.AddWithValue("summary", cipher.Summary);
        command.Parameters.AddWithValue("description", cipher.Description);
        command.Parameters.AddWithValue("example", cipher.Example);
        command.Parameters.AddWithValue("publication_status", cipher.PublicationStatus.ToString());
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task ReplaceCipherRelatedEventsAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, CipherCard cipher, CancellationToken cancellationToken)
    {
        const string deleteSql = "DELETE FROM content.cipher_related_events WHERE cipher_id = @cipher_id;";
        await using (var deleteCommand = new NpgsqlCommand(deleteSql, connection, transaction))
        {
            deleteCommand.Parameters.AddWithValue("cipher_id", cipher.Id);
            await deleteCommand.ExecuteNonQueryAsync(cancellationToken);
        }

        const string insertSql = "INSERT INTO content.cipher_related_events (cipher_id, event_id) VALUES (@cipher_id, @event_id);";
        foreach (var eventId in cipher.RelatedEventIds)
        {
            await using var insertCommand = new NpgsqlCommand(insertSql, connection, transaction);
            insertCommand.Parameters.AddWithValue("cipher_id", cipher.Id);
            insertCommand.Parameters.AddWithValue("event_id", eventId);
            await insertCommand.ExecuteNonQueryAsync(cancellationToken);
        }
    }

    private static async Task ReplaceCipherVersionsAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, CipherCard cipher, CancellationToken cancellationToken)
    {
        const string deleteSql = "DELETE FROM content.cipher_versions WHERE cipher_id = @cipher_id;";
        await using (var deleteCommand = new NpgsqlCommand(deleteSql, connection, transaction))
        {
            deleteCommand.Parameters.AddWithValue("cipher_id", cipher.Id);
            await deleteCommand.ExecuteNonQueryAsync(cancellationToken);
        }

        const string insertSql = """
            INSERT INTO content.cipher_versions (cipher_id, version_number, edited_by, updated_at, change_summary)
            VALUES (@cipher_id, @version_number, @edited_by, @updated_at, @change_summary);
            """;

        foreach (var version in cipher.Versions)
        {
            await using var insertCommand = new NpgsqlCommand(insertSql, connection, transaction);
            insertCommand.Parameters.AddWithValue("cipher_id", cipher.Id);
            insertCommand.Parameters.AddWithValue("version_number", version.VersionNumber);
            insertCommand.Parameters.AddWithValue("edited_by", version.EditedBy);
            insertCommand.Parameters.AddWithValue("updated_at", version.UpdatedAt);
            insertCommand.Parameters.AddWithValue("change_summary", version.ChangeSummary);
            await insertCommand.ExecuteNonQueryAsync(cancellationToken);
        }
    }

    private static async Task UpsertEventAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, HistoricalEvent historicalEvent, CancellationToken cancellationToken)
    {
        const string sql = """
            INSERT INTO content.historical_events (id, title, event_date, region, topic, summary, description, publication_status, updated_at)
            VALUES (@id, @title, @event_date, @region, @topic, @summary, @description, @publication_status, now())
            ON CONFLICT (id) DO UPDATE
            SET title = EXCLUDED.title,
                event_date = EXCLUDED.event_date,
                region = EXCLUDED.region,
                topic = EXCLUDED.topic,
                summary = EXCLUDED.summary,
                description = EXCLUDED.description,
                publication_status = EXCLUDED.publication_status,
                updated_at = now();
            """;

        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("id", historicalEvent.Id);
        command.Parameters.AddWithValue("title", historicalEvent.Title);
        command.Parameters.AddWithValue("event_date", historicalEvent.Date);
        command.Parameters.AddWithValue("region", historicalEvent.Region);
        command.Parameters.AddWithValue("topic", historicalEvent.Topic);
        command.Parameters.AddWithValue("summary", historicalEvent.Summary);
        command.Parameters.AddWithValue("description", historicalEvent.Description);
        command.Parameters.AddWithValue("publication_status", historicalEvent.PublicationStatus.ToString());
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task ReplaceEventParticipantsAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, HistoricalEvent historicalEvent, CancellationToken cancellationToken)
    {
        const string deleteSql = "DELETE FROM content.event_participants WHERE event_id = @event_id;";
        await using (var deleteCommand = new NpgsqlCommand(deleteSql, connection, transaction))
        {
            deleteCommand.Parameters.AddWithValue("event_id", historicalEvent.Id);
            await deleteCommand.ExecuteNonQueryAsync(cancellationToken);
        }

        const string insertSql = "INSERT INTO content.event_participants (event_id, participant) VALUES (@event_id, @participant);";
        foreach (var participant in historicalEvent.Participants)
        {
            await using var insertCommand = new NpgsqlCommand(insertSql, connection, transaction);
            insertCommand.Parameters.AddWithValue("event_id", historicalEvent.Id);
            insertCommand.Parameters.AddWithValue("participant", participant);
            await insertCommand.ExecuteNonQueryAsync(cancellationToken);
        }
    }

    private static async Task ReplaceEventCipherCodesAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, HistoricalEvent historicalEvent, CancellationToken cancellationToken)
    {
        const string deleteSql = "DELETE FROM content.event_cipher_codes WHERE event_id = @event_id;";
        await using (var deleteCommand = new NpgsqlCommand(deleteSql, connection, transaction))
        {
            deleteCommand.Parameters.AddWithValue("event_id", historicalEvent.Id);
            await deleteCommand.ExecuteNonQueryAsync(cancellationToken);
        }

        const string insertSql = "INSERT INTO content.event_cipher_codes (event_id, cipher_code) VALUES (@event_id, @cipher_code);";
        foreach (var cipherCode in historicalEvent.CipherCodes)
        {
            await using var insertCommand = new NpgsqlCommand(insertSql, connection, transaction);
            insertCommand.Parameters.AddWithValue("event_id", historicalEvent.Id);
            insertCommand.Parameters.AddWithValue("cipher_code", cipherCode);
            await insertCommand.ExecuteNonQueryAsync(cancellationToken);
        }
    }

    private static async Task UpsertCollectionAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, CuratedCollection collection, CancellationToken cancellationToken)
    {
        const string sql = """
            INSERT INTO content.collections (id, title, theme, summary, publication_status, updated_at)
            VALUES (@id, @title, @theme, @summary, @publication_status, now())
            ON CONFLICT (id) DO UPDATE
            SET title = EXCLUDED.title,
                theme = EXCLUDED.theme,
                summary = EXCLUDED.summary,
                publication_status = EXCLUDED.publication_status,
                updated_at = now();
            """;

        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("id", collection.Id);
        command.Parameters.AddWithValue("title", collection.Title);
        command.Parameters.AddWithValue("theme", collection.Theme);
        command.Parameters.AddWithValue("summary", collection.Summary);
        command.Parameters.AddWithValue("publication_status", collection.PublicationStatus.ToString());
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task ReplaceCollectionEventsAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, CuratedCollection collection, CancellationToken cancellationToken)
    {
        const string deleteSql = "DELETE FROM content.collection_events WHERE collection_id = @collection_id;";
        await using (var deleteCommand = new NpgsqlCommand(deleteSql, connection, transaction))
        {
            deleteCommand.Parameters.AddWithValue("collection_id", collection.Id);
            await deleteCommand.ExecuteNonQueryAsync(cancellationToken);
        }

        const string insertSql = "INSERT INTO content.collection_events (collection_id, event_id) VALUES (@collection_id, @event_id);";
        foreach (var eventId in collection.EventIds)
        {
            await using var insertCommand = new NpgsqlCommand(insertSql, connection, transaction);
            insertCommand.Parameters.AddWithValue("collection_id", collection.Id);
            insertCommand.Parameters.AddWithValue("event_id", eventId);
            await insertCommand.ExecuteNonQueryAsync(cancellationToken);
        }
    }

    private static async Task ReplaceCollectionCipherCodesAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, CuratedCollection collection, CancellationToken cancellationToken)
    {
        const string deleteSql = "DELETE FROM content.collection_cipher_codes WHERE collection_id = @collection_id;";
        await using (var deleteCommand = new NpgsqlCommand(deleteSql, connection, transaction))
        {
            deleteCommand.Parameters.AddWithValue("collection_id", collection.Id);
            await deleteCommand.ExecuteNonQueryAsync(cancellationToken);
        }

        const string insertSql = "INSERT INTO content.collection_cipher_codes (collection_id, cipher_code) VALUES (@collection_id, @cipher_code);";
        foreach (var cipherCode in collection.CipherCodes)
        {
            await using var insertCommand = new NpgsqlCommand(insertSql, connection, transaction);
            insertCommand.Parameters.AddWithValue("collection_id", collection.Id);
            insertCommand.Parameters.AddWithValue("cipher_code", cipherCode);
            await insertCommand.ExecuteNonQueryAsync(cancellationToken);
        }
    }

    private sealed record CipherRow(
        Guid Id,
        string Code,
        string Name,
        string Category,
        string Era,
        int Difficulty,
        string Summary,
        string Description,
        string Example,
        PublicationStatus Status);

    private sealed record EventRow(
        Guid Id,
        string Title,
        DateOnly EventDate,
        string Region,
        string Topic,
        string Summary,
        string Description,
        PublicationStatus Status);

    private sealed record CollectionRow(
        Guid Id,
        string Title,
        string Theme,
        string Summary,
        PublicationStatus Status);
}
