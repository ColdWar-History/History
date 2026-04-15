using System.Data;
using System.Text;
using ColdWarHistory.BuildingBlocks.Infrastructure;
using Npgsql;

var configuration = new MigrationConfiguration(
    Environment.GetEnvironmentVariable("MIGRATOR__Mode") ?? "up",
    ParseList(Environment.GetEnvironmentVariable("MIGRATOR__Services"), ["auth", "content", "game", "progress"]),
    Environment.GetEnvironmentVariable("MIGRATOR__Steps") is { Length: > 0 } rawSteps && int.TryParse(rawSteps, out var steps) ? Math.Max(1, steps) : 1,
    Environment.GetEnvironmentVariable("ConnectionStrings__Default")
        ?? "Host=localhost;Port=5432;Database=coldwarhistory;Username=postgres;Password=postgres");

var backendRoot = WorkspacePaths.ResolveBackendRoot();
var migrationsRoot = Path.Combine(backendRoot, "database", "migrations");

await using var dataSource = NpgsqlDataSource.Create(configuration.ConnectionString);
await using var connection = await dataSource.OpenConnectionAsync();

await EnsureMigrationTableAsync(connection);

if (string.Equals(configuration.Mode, "down", StringComparison.OrdinalIgnoreCase))
{
    await ApplyDownAsync(connection, migrationsRoot, configuration.Services, configuration.Steps);
}
else
{
    await ApplyUpAsync(connection, migrationsRoot, configuration.Services);
}

Console.WriteLine("Database migrations finished successfully.");

static async Task ApplyUpAsync(NpgsqlConnection connection, string migrationsRoot, IReadOnlyCollection<string> services)
{
    foreach (var service in services)
    {
        var serviceFolder = Path.Combine(migrationsRoot, service, "up");
        if (!Directory.Exists(serviceFolder))
        {
            throw new DirectoryNotFoundException($"Migration folder not found: {serviceFolder}");
        }

        var scripts = Directory.GetFiles(serviceFolder, "*.sql")
            .OrderBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        foreach (var scriptPath in scripts)
        {
            var migrationId = Path.GetFileName(scriptPath);
            if (await IsAppliedAsync(connection, service, migrationId))
            {
                continue;
            }

            var sql = await File.ReadAllTextAsync(scriptPath, Encoding.UTF8);
            await using var transaction = await connection.BeginTransactionAsync();

            await ExecuteSqlAsync(connection, transaction, sql);
            await MarkAppliedAsync(connection, transaction, service, migrationId);

            await transaction.CommitAsync();
            Console.WriteLine($"[up] {service}/{migrationId}");
        }
    }
}

static async Task ApplyDownAsync(NpgsqlConnection connection, string migrationsRoot, IReadOnlyCollection<string> services, int steps)
{
    foreach (var service in services)
    {
        var downFolder = Path.Combine(migrationsRoot, service, "down");
        if (!Directory.Exists(downFolder))
        {
            throw new DirectoryNotFoundException($"Rollback folder not found: {downFolder}");
        }

        var applied = await GetAppliedAsync(connection, service, steps);
        foreach (var migrationId in applied)
        {
            var scriptPath = Path.Combine(downFolder, migrationId);
            if (!File.Exists(scriptPath))
            {
                throw new FileNotFoundException($"Rollback script not found for {service}/{migrationId}", scriptPath);
            }

            var sql = await File.ReadAllTextAsync(scriptPath, Encoding.UTF8);
            await using var transaction = await connection.BeginTransactionAsync();

            await ExecuteSqlAsync(connection, transaction, sql);
            await MarkRolledBackAsync(connection, transaction, service, migrationId);

            await transaction.CommitAsync();
            Console.WriteLine($"[down] {service}/{migrationId}");
        }
    }
}

static async Task EnsureMigrationTableAsync(NpgsqlConnection connection)
{
    const string sql = """
        CREATE TABLE IF NOT EXISTS public.schema_migrations
        (
            service_name text NOT NULL,
            migration_id text NOT NULL,
            applied_at timestamptz NOT NULL DEFAULT now(),
            PRIMARY KEY (service_name, migration_id)
        );
        """;

    await using var command = new NpgsqlCommand(sql, connection);
    await command.ExecuteNonQueryAsync();
}

static async Task<bool> IsAppliedAsync(NpgsqlConnection connection, string service, string migrationId)
{
    const string sql = """
        SELECT 1
        FROM public.schema_migrations
        WHERE service_name = @service AND migration_id = @migration_id;
        """;

    await using var command = new NpgsqlCommand(sql, connection);
    command.Parameters.AddWithValue("service", service);
    command.Parameters.AddWithValue("migration_id", migrationId);

    var result = await command.ExecuteScalarAsync();
    return result is not null;
}

static async Task<IReadOnlyCollection<string>> GetAppliedAsync(NpgsqlConnection connection, string service, int steps)
{
    const string sql = """
        SELECT migration_id
        FROM public.schema_migrations
        WHERE service_name = @service
        ORDER BY migration_id DESC
        LIMIT @limit;
        """;

    await using var command = new NpgsqlCommand(sql, connection);
    command.Parameters.AddWithValue("service", service);
    command.Parameters.AddWithValue("limit", steps);

    var results = new List<string>();
    await using var reader = await command.ExecuteReaderAsync();
    while (await reader.ReadAsync())
    {
        results.Add(reader.GetString(0));
    }

    return results;
}

static async Task ExecuteSqlAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, string sql)
{
    await using var command = new NpgsqlCommand(sql, connection, transaction)
    {
        CommandType = CommandType.Text
    };

    await command.ExecuteNonQueryAsync();
}

static async Task MarkAppliedAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, string service, string migrationId)
{
    const string sql = """
        INSERT INTO public.schema_migrations (service_name, migration_id)
        VALUES (@service, @migration_id);
        """;

    await using var command = new NpgsqlCommand(sql, connection, transaction);
    command.Parameters.AddWithValue("service", service);
    command.Parameters.AddWithValue("migration_id", migrationId);
    await command.ExecuteNonQueryAsync();
}

static async Task MarkRolledBackAsync(NpgsqlConnection connection, NpgsqlTransaction transaction, string service, string migrationId)
{
    const string sql = """
        DELETE FROM public.schema_migrations
        WHERE service_name = @service AND migration_id = @migration_id;
        """;

    await using var command = new NpgsqlCommand(sql, connection, transaction);
    command.Parameters.AddWithValue("service", service);
    command.Parameters.AddWithValue("migration_id", migrationId);
    await command.ExecuteNonQueryAsync();
}

static IReadOnlyCollection<string> ParseList(string? value, IReadOnlyCollection<string> fallback)
{
    if (string.IsNullOrWhiteSpace(value))
    {
        return fallback;
    }

    var parsed = value
        .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
        .Where(item => item.Length > 0)
        .ToArray();

    return parsed.Length == 0 ? fallback : parsed;
}

file sealed record MigrationConfiguration(string Mode, IReadOnlyCollection<string> Services, int Steps, string ConnectionString);
