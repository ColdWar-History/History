using ColdWarHistory.BuildingBlocks.Domain;

namespace ColdWarHistory.Content.Domain;

public enum PublicationStatus
{
    Draft,
    Published,
    Archived
}

public sealed record ContentVersion(int VersionNumber, string EditedBy, DateTimeOffset UpdatedAt, string ChangeSummary);

public sealed class CipherCard : AggregateRoot<Guid>
{
    private readonly List<ContentVersion> _versions = [];
    private readonly List<Guid> _relatedEventIds = [];

    public CipherCard(
        Guid id,
        string code,
        string name,
        string category,
        string era,
        int difficulty,
        string summary,
        string description,
        string example,
        IEnumerable<Guid> relatedEventIds,
        PublicationStatus publicationStatus)
        : base(id)
    {
        Code = code;
        Name = name;
        Category = category;
        Era = era;
        Difficulty = difficulty;
        Summary = summary;
        Description = description;
        Example = example;
        PublicationStatus = publicationStatus;
        _relatedEventIds.AddRange(relatedEventIds);
    }

    public string Code { get; private set; }
    public string Name { get; private set; }
    public string Category { get; private set; }
    public string Era { get; private set; }
    public int Difficulty { get; private set; }
    public string Summary { get; private set; }
    public string Description { get; private set; }
    public string Example { get; private set; }
    public PublicationStatus PublicationStatus { get; private set; }
    public IReadOnlyCollection<Guid> RelatedEventIds => _relatedEventIds;
    public IReadOnlyCollection<ContentVersion> Versions => _versions;

    public void Update(string name, string category, string era, int difficulty, string summary, string description, string example, IEnumerable<Guid> relatedEventIds)
    {
        Name = name;
        Category = category;
        Era = era;
        Difficulty = difficulty;
        Summary = summary;
        Description = description;
        Example = example;
        _relatedEventIds.Clear();
        _relatedEventIds.AddRange(relatedEventIds);
    }

    public void SetPublicationStatus(PublicationStatus status) => PublicationStatus = status;

    public void AddVersion(string editedBy, string changeSummary) =>
        _versions.Add(new ContentVersion(_versions.Count + 1, editedBy, DateTimeOffset.UtcNow, changeSummary));
}

public sealed class HistoricalEvent : AggregateRoot<Guid>
{
    private readonly List<string> _participants = [];
    private readonly List<string> _cipherCodes = [];

    public HistoricalEvent(
        Guid id,
        string title,
        DateOnly date,
        string region,
        string topic,
        string summary,
        string description,
        IEnumerable<string> participants,
        IEnumerable<string> cipherCodes,
        PublicationStatus publicationStatus)
        : base(id)
    {
        Title = title;
        Date = date;
        Region = region;
        Topic = topic;
        Summary = summary;
        Description = description;
        PublicationStatus = publicationStatus;
        _participants.AddRange(participants);
        _cipherCodes.AddRange(cipherCodes);
    }

    public string Title { get; private set; }
    public DateOnly Date { get; private set; }
    public string Region { get; private set; }
    public string Topic { get; private set; }
    public string Summary { get; private set; }
    public string Description { get; private set; }
    public PublicationStatus PublicationStatus { get; private set; }
    public IReadOnlyCollection<string> Participants => _participants;
    public IReadOnlyCollection<string> CipherCodes => _cipherCodes;

    public void Update(string title, DateOnly date, string region, string topic, string summary, string description, IEnumerable<string> participants, IEnumerable<string> cipherCodes)
    {
        Title = title;
        Date = date;
        Region = region;
        Topic = topic;
        Summary = summary;
        Description = description;
        _participants.Clear();
        _participants.AddRange(participants);
        _cipherCodes.Clear();
        _cipherCodes.AddRange(cipherCodes);
    }

    public void SetPublicationStatus(PublicationStatus status) => PublicationStatus = status;
}

public sealed class CuratedCollection : AggregateRoot<Guid>
{
    private readonly List<Guid> _eventIds = [];
    private readonly List<string> _cipherCodes = [];

    public CuratedCollection(
        Guid id,
        string title,
        string theme,
        string summary,
        IEnumerable<Guid> eventIds,
        IEnumerable<string> cipherCodes,
        PublicationStatus publicationStatus)
        : base(id)
    {
        Title = title;
        Theme = theme;
        Summary = summary;
        PublicationStatus = publicationStatus;
        _eventIds.AddRange(eventIds);
        _cipherCodes.AddRange(cipherCodes);
    }

    public string Title { get; private set; }
    public string Theme { get; private set; }
    public string Summary { get; private set; }
    public PublicationStatus PublicationStatus { get; private set; }
    public IReadOnlyCollection<Guid> EventIds => _eventIds;
    public IReadOnlyCollection<string> CipherCodes => _cipherCodes;

    public void Update(string title, string theme, string summary, IEnumerable<Guid> eventIds, IEnumerable<string> cipherCodes)
    {
        Title = title;
        Theme = theme;
        Summary = summary;
        _eventIds.Clear();
        _eventIds.AddRange(eventIds);
        _cipherCodes.Clear();
        _cipherCodes.AddRange(cipherCodes);
    }

    public void SetPublicationStatus(PublicationStatus status) => PublicationStatus = status;
}
