using ColdWarHistory.BuildingBlocks.Application;
using ColdWarHistory.BuildingBlocks.Contracts;
using ColdWarHistory.Content.Domain;

namespace ColdWarHistory.Content.Application;

public interface IContentRepository
{
    Task<IReadOnlyCollection<CipherCard>> GetCiphersAsync(CancellationToken cancellationToken);
    Task<CipherCard?> GetCipherAsync(Guid id, CancellationToken cancellationToken);
    Task AddCipherAsync(CipherCard cipher, CancellationToken cancellationToken);

    Task<IReadOnlyCollection<HistoricalEvent>> GetEventsAsync(CancellationToken cancellationToken);
    Task<HistoricalEvent?> GetEventAsync(Guid id, CancellationToken cancellationToken);
    Task AddEventAsync(HistoricalEvent historicalEvent, CancellationToken cancellationToken);

    Task<IReadOnlyCollection<CuratedCollection>> GetCollectionsAsync(CancellationToken cancellationToken);
    Task<CuratedCollection?> GetCollectionAsync(Guid id, CancellationToken cancellationToken);
    Task AddCollectionAsync(CuratedCollection collection, CancellationToken cancellationToken);

    Task SaveChangesAsync(CancellationToken cancellationToken);
}

public interface IContentService
{
    Task<IReadOnlyCollection<CipherCardDto>> GetCiphersAsync(string? search, string? category, string? era, bool publishedOnly, CancellationToken cancellationToken);
    Task<CipherCardDto?> GetCipherAsync(Guid id, CancellationToken cancellationToken);
    Task<CipherCardDto> UpsertCipherAsync(Guid? id, UpsertCipherCardRequest request, CancellationToken cancellationToken);
    Task PublishCipherAsync(Guid id, string status, CancellationToken cancellationToken);

    Task<IReadOnlyCollection<HistoricalEventDto>> GetEventsAsync(string? region, int? year, string? topic, bool publishedOnly, CancellationToken cancellationToken);
    Task<HistoricalEventDto?> GetEventAsync(Guid id, CancellationToken cancellationToken);
    Task<HistoricalEventDto> UpsertEventAsync(Guid? id, UpsertHistoricalEventRequest request, CancellationToken cancellationToken);
    Task PublishEventAsync(Guid id, string status, CancellationToken cancellationToken);

    Task<IReadOnlyCollection<CollectionDto>> GetCollectionsAsync(bool publishedOnly, CancellationToken cancellationToken);
    Task<CollectionDto?> GetCollectionAsync(Guid id, CancellationToken cancellationToken);
    Task<CollectionDto> UpsertCollectionAsync(Guid? id, UpsertCollectionRequest request, CancellationToken cancellationToken);
    Task PublishCollectionAsync(Guid id, string status, CancellationToken cancellationToken);
}
