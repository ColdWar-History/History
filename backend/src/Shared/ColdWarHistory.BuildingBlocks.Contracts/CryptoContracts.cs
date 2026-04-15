namespace ColdWarHistory.BuildingBlocks.Contracts;

public sealed record CipherParameterDefinition(string Name, string Label, string Type, bool IsRequired, string? Description);

public sealed record CipherCatalogItem(string Code, string Name, string Category, string Era, int Difficulty, IReadOnlyCollection<CipherParameterDefinition> Parameters);

public sealed record CryptoTransformRequest(
    string CipherCode,
    string Mode,
    string Input,
    Dictionary<string, string> Parameters,
    string? ExplanationLevel = null);

public sealed record CryptoStepDto(int Order, string Title, string Description, string Snapshot);

public sealed record CryptoTransformResponse(
    string CipherCode,
    string Mode,
    string Input,
    string Output,
    IReadOnlyCollection<CryptoStepDto> Steps,
    IReadOnlyCollection<string> ValidationMessages,
    DateTimeOffset ProcessedAt,
    Guid? OperationId = null);

public sealed record CryptoOperationRecordedEvent(
    Guid OperationId,
    Guid UserId,
    string UserName,
    string CipherCode,
    string Mode,
    string Input,
    string Output,
    DateTimeOffset ProcessedAt);
