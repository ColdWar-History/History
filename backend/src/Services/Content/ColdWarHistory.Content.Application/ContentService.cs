using ColdWarHistory.BuildingBlocks.Contracts;
using ColdWarHistory.Content.Domain;

namespace ColdWarHistory.Content.Application;

public sealed class ContentService(IContentRepository repository) : IContentService
{
    public async Task<IReadOnlyCollection<CipherCardDto>> GetCiphersAsync(string? search, string? category, string? era, bool publishedOnly, CancellationToken cancellationToken)
    {
        var items = await repository.GetCiphersAsync(cancellationToken);
        var query = items.AsEnumerable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(item =>
                item.Name.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                item.Code.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                item.Summary.Contains(search, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(category))
        {
            query = query.Where(item => string.Equals(item.Category, category, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(era))
        {
            query = query.Where(item => string.Equals(item.Era, era, StringComparison.OrdinalIgnoreCase));
        }

        if (publishedOnly)
        {
            query = query.Where(item => item.PublicationStatus == PublicationStatus.Published);
        }

        return query.Select(Map).ToArray();
    }

    public async Task<CipherCardDto?> GetCipherAsync(Guid id, CancellationToken cancellationToken)
    {
        var item = await repository.GetCipherAsync(id, cancellationToken);
        return item is null ? null : Map(item);
    }

    public async Task<CipherCardDto> UpsertCipherAsync(Guid? id, UpsertCipherCardRequest request, CancellationToken cancellationToken)
    {
        CipherCard entity;
        if (id.HasValue)
        {
            entity = await repository.GetCipherAsync(id.Value, cancellationToken)
                ?? throw new InvalidOperationException("Cipher not found.");

            entity.Update(request.Name, request.Category, request.Era, request.Difficulty, request.Summary, request.Description, request.Example, request.RelatedEventIds);
        }
        else
        {
            entity = new CipherCard(Guid.NewGuid(), request.Code, request.Name, request.Category, request.Era, request.Difficulty, request.Summary, request.Description, request.Example, request.RelatedEventIds, PublicationStatus.Draft);
            await repository.AddCipherAsync(entity, cancellationToken);
        }

        entity.AddVersion(request.EditedBy, request.ChangeSummary);
        await repository.SaveChangesAsync(cancellationToken);
        return Map(entity);
    }

    public async Task PublishCipherAsync(Guid id, string status, CancellationToken cancellationToken)
    {
        var entity = await repository.GetCipherAsync(id, cancellationToken)
            ?? throw new InvalidOperationException("Cipher not found.");

        entity.SetPublicationStatus(ParseStatus(status));
        await repository.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyCollection<HistoricalEventDto>> GetEventsAsync(string? region, int? year, string? topic, bool publishedOnly, CancellationToken cancellationToken)
    {
        var items = await repository.GetEventsAsync(cancellationToken);
        var query = items.AsEnumerable();

        if (!string.IsNullOrWhiteSpace(region))
        {
            query = query.Where(item => string.Equals(item.Region, region, StringComparison.OrdinalIgnoreCase));
        }

        if (year.HasValue)
        {
            query = query.Where(item => item.Date.Year == year);
        }

        if (!string.IsNullOrWhiteSpace(topic))
        {
            query = query.Where(item => string.Equals(item.Topic, topic, StringComparison.OrdinalIgnoreCase));
        }

        if (publishedOnly)
        {
            query = query.Where(item => item.PublicationStatus == PublicationStatus.Published);
        }

        return query.OrderBy(item => item.Date).Select(Map).ToArray();
    }

    public async Task<HistoricalEventDto?> GetEventAsync(Guid id, CancellationToken cancellationToken)
    {
        var item = await repository.GetEventAsync(id, cancellationToken);
        return item is null ? null : Map(item);
    }

    public async Task<HistoricalEventDto> UpsertEventAsync(Guid? id, UpsertHistoricalEventRequest request, CancellationToken cancellationToken)
    {
        HistoricalEvent entity;
        if (id.HasValue)
        {
            entity = await repository.GetEventAsync(id.Value, cancellationToken)
                ?? throw new InvalidOperationException("Event not found.");

            entity.Update(request.Title, request.Date, request.Region, request.Topic, request.Summary, request.Description, request.Participants, request.CipherCodes);
        }
        else
        {
            entity = new HistoricalEvent(Guid.NewGuid(), request.Title, request.Date, request.Region, request.Topic, request.Summary, request.Description, request.Participants, request.CipherCodes, PublicationStatus.Draft);
            await repository.AddEventAsync(entity, cancellationToken);
        }

        await repository.SaveChangesAsync(cancellationToken);
        return Map(entity);
    }

    public async Task PublishEventAsync(Guid id, string status, CancellationToken cancellationToken)
    {
        var entity = await repository.GetEventAsync(id, cancellationToken)
            ?? throw new InvalidOperationException("Event not found.");

        entity.SetPublicationStatus(ParseStatus(status));
        await repository.SaveChangesAsync(cancellationToken);
    }

    public async Task<IReadOnlyCollection<CollectionDto>> GetCollectionsAsync(bool publishedOnly, CancellationToken cancellationToken)
    {
        var items = await repository.GetCollectionsAsync(cancellationToken);
        if (publishedOnly)
        {
            items = items.Where(item => item.PublicationStatus == PublicationStatus.Published).ToArray();
        }

        return items.Select(Map).ToArray();
    }

    public async Task<CollectionDto?> GetCollectionAsync(Guid id, CancellationToken cancellationToken)
    {
        var item = await repository.GetCollectionAsync(id, cancellationToken);
        return item is null ? null : Map(item);
    }

    public async Task<CollectionDto> UpsertCollectionAsync(Guid? id, UpsertCollectionRequest request, CancellationToken cancellationToken)
    {
        CuratedCollection entity;
        if (id.HasValue)
        {
            entity = await repository.GetCollectionAsync(id.Value, cancellationToken)
                ?? throw new InvalidOperationException("Collection not found.");

            entity.Update(request.Title, request.Theme, request.Summary, request.EventIds, request.CipherCodes);
        }
        else
        {
            entity = new CuratedCollection(Guid.NewGuid(), request.Title, request.Theme, request.Summary, request.EventIds, request.CipherCodes, PublicationStatus.Draft);
            await repository.AddCollectionAsync(entity, cancellationToken);
        }

        await repository.SaveChangesAsync(cancellationToken);
        return Map(entity);
    }

    public async Task PublishCollectionAsync(Guid id, string status, CancellationToken cancellationToken)
    {
        var entity = await repository.GetCollectionAsync(id, cancellationToken)
            ?? throw new InvalidOperationException("Collection not found.");

        entity.SetPublicationStatus(ParseStatus(status));
        await repository.SaveChangesAsync(cancellationToken);
    }

    private static PublicationStatus ParseStatus(string status) =>
        Enum.TryParse<PublicationStatus>(status, true, out var parsedStatus)
            ? parsedStatus
            : throw new InvalidOperationException("Unsupported publication status.");

    private static CipherCardDto Map(CipherCard item) =>
        new(
            item.Id,
            item.Code,
            item.Name,
            item.Category,
            item.Era,
            item.Difficulty,
            item.Summary,
            item.Description,
            item.Example,
            item.PublicationStatus.ToString(),
            item.RelatedEventIds.ToArray(),
            item.Versions.Select(version => new ContentVersionDto(version.VersionNumber, version.EditedBy, version.UpdatedAt, version.ChangeSummary)).ToArray());

    private static HistoricalEventDto Map(HistoricalEvent item) =>
        new(
            item.Id,
            item.Title,
            item.Date,
            item.Region,
            item.Topic,
            item.Summary,
            item.Description,
            item.Participants.ToArray(),
            item.CipherCodes.ToArray(),
            item.PublicationStatus.ToString());

    private static CollectionDto Map(CuratedCollection item) =>
        new(
            item.Id,
            item.Title,
            item.Theme,
            item.Summary,
            item.EventIds.ToArray(),
            item.CipherCodes.ToArray(),
            item.PublicationStatus.ToString());
}
