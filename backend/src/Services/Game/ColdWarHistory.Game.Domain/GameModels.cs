using ColdWarHistory.BuildingBlocks.Domain;

namespace ColdWarHistory.Game.Domain;

public sealed class TrainingChallenge(
    Guid id,
    string cipherCode,
    string difficulty,
    string prompt,
    string input,
    string expectedMode,
    IReadOnlyDictionary<string, string> parameters,
    string expectedAnswer,
    int baseScore,
    DateTimeOffset generatedAt) : AggregateRoot<Guid>(id)
{
    public string CipherCode { get; } = cipherCode;
    public string Difficulty { get; } = difficulty;
    public string Prompt { get; } = prompt;
    public string Input { get; } = input;
    public string ExpectedMode { get; } = expectedMode;
    public IReadOnlyDictionary<string, string> Parameters { get; } = parameters;
    public string ExpectedAnswer { get; } = expectedAnswer;
    public int BaseScore { get; } = baseScore;
    public DateTimeOffset GeneratedAt { get; } = generatedAt;
}

public sealed class ShiftMessage(Guid id, string headline, string encodedMessage, string cipherCode, string briefing, string expectedDecision) : Entity<Guid>(id)
{
    public string Headline { get; } = headline;
    public string EncodedMessage { get; } = encodedMessage;
    public string CipherCode { get; } = cipherCode;
    public string Briefing { get; } = briefing;
    public string ExpectedDecision { get; } = expectedDecision;
}

public sealed class ShiftSession(Guid id, string difficulty, IReadOnlyCollection<ShiftMessage> messages, DateTimeOffset startedAt) : AggregateRoot<Guid>(id)
{
    private readonly Dictionary<Guid, ShiftResolution> _resolutions = [];

    public string Difficulty { get; } = difficulty;
    public IReadOnlyCollection<ShiftMessage> Messages { get; } = messages;
    public DateTimeOffset StartedAt { get; } = startedAt;
    public IReadOnlyDictionary<Guid, ShiftResolution> Resolutions => _resolutions;

    public void RecordResolution(ShiftResolution resolution) => _resolutions[resolution.MessageId] = resolution;

    public bool IsCompleted => Messages.All(message => _resolutions.ContainsKey(message.Id));
}

public sealed record ShiftResolution(Guid MessageId, string Decision, bool IsCorrect, int ScoreDelta, string Explanation);

public sealed record DailyChallengeSnapshot(DateOnly Date, TrainingChallenge Challenge, string Theme);
