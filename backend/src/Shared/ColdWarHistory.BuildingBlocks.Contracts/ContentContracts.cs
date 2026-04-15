namespace ColdWarHistory.BuildingBlocks.Contracts;

public sealed record CipherCardDto(
    Guid Id,
    string Code,
    string Name,
    string Category,
    string Era,
    int Difficulty,
    string Summary,
    string Description,
    string Example,
    string PublicationStatus,
    IReadOnlyCollection<Guid> RelatedEventIds,
    IReadOnlyCollection<ContentVersionDto> Versions);

public sealed record HistoricalEventDto(
    Guid Id,
    string Title,
    DateOnly Date,
    string Region,
    string Topic,
    string Summary,
    string Description,
    IReadOnlyCollection<string> Participants,
    IReadOnlyCollection<string> CipherCodes,
    string PublicationStatus);

public sealed record CollectionDto(
    Guid Id,
    string Title,
    string Theme,
    string Summary,
    IReadOnlyCollection<Guid> EventIds,
    IReadOnlyCollection<string> CipherCodes,
    string PublicationStatus);

public sealed record ContentVersionDto(int VersionNumber, string EditedBy, DateTimeOffset UpdatedAt, string ChangeSummary);

public sealed record UpsertCipherCardRequest(
    string Code,
    string Name,
    string Category,
    string Era,
    int Difficulty,
    string Summary,
    string Description,
    string Example,
    IReadOnlyCollection<Guid> RelatedEventIds,
    string EditedBy,
    string ChangeSummary);

public sealed record UpsertHistoricalEventRequest(
    string Title,
    DateOnly Date,
    string Region,
    string Topic,
    string Summary,
    string Description,
    IReadOnlyCollection<string> Participants,
    IReadOnlyCollection<string> CipherCodes);

public sealed record UpsertCollectionRequest(
    string Title,
    string Theme,
    string Summary,
    IReadOnlyCollection<Guid> EventIds,
    IReadOnlyCollection<string> CipherCodes);
