namespace ColdWarHistory.BuildingBlocks.Application;

public sealed record AuditRecord(
    DateTimeOffset Timestamp,
    string Service,
    string Action,
    string Subject,
    Guid? UserId,
    string? UserName,
    IReadOnlyDictionary<string, string?> Details);

public interface IAuditSink
{
    Task WriteAsync(AuditRecord record, CancellationToken cancellationToken = default);
}
