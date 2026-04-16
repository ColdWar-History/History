using System.Text.Json;
using System.Text;
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
            await using var stream = new FileStream(
                _filePath,
                FileMode.Append,
                FileAccess.Write,
                FileShare.ReadWrite,
                bufferSize: 4096,
                useAsync: true);

            await using var writer = new StreamWriter(stream, Encoding.UTF8);
            await writer.WriteLineAsync(line.AsMemory(), cancellationToken);
        }
        finally
        {
            _semaphore.Release();
        }
    }
}
