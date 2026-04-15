using System.Text.Json;
using ColdWarHistory.BuildingBlocks.Application;

namespace ColdWarHistory.BuildingBlocks.Infrastructure;

public sealed class JsonLinesAuditSink : IAuditSink
{
    private static readonly JsonSerializerOptions SerializerOptions = new(JsonSerializerDefaults.Web);
    private readonly SemaphoreSlim _semaphore = new(1, 1);
    private readonly string _filePath;

    public JsonLinesAuditSink()
    {
        var directory = WorkspacePaths.ResolveRuntimePath("audit");
        Directory.CreateDirectory(directory);
        _filePath = Path.Combine(directory, "audit-log.jsonl");
    }

    public async Task WriteAsync(AuditRecord record, CancellationToken cancellationToken = default)
    {
        var line = JsonSerializer.Serialize(record, SerializerOptions);

        await _semaphore.WaitAsync(cancellationToken);
        try
        {
            await File.AppendAllTextAsync(_filePath, line + Environment.NewLine, cancellationToken);
        }
        finally
        {
            _semaphore.Release();
        }
    }
}
