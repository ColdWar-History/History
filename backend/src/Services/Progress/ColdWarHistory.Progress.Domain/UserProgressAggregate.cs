using ColdWarHistory.BuildingBlocks.Domain;

namespace ColdWarHistory.Progress.Domain;

public sealed class UserProgressAggregate(Guid id, string userName) : AggregateRoot<Guid>(id)
{
    private readonly List<CryptoOperationEntry> _operations = [];
    private readonly List<AchievementEntry> _achievements = [];

    public string UserName { get; private set; } = userName;
    public int TotalScore { get; private set; }
    public int ChallengesCompleted { get; private set; }
    public int CorrectChallenges { get; private set; }
    public int ShiftReportsCompleted { get; private set; }
    public IReadOnlyCollection<CryptoOperationEntry> Operations => _operations;
    public IReadOnlyCollection<AchievementEntry> Achievements => _achievements;

    public void Rename(string userName) => UserName = userName;

    public void RecordOperation(CryptoOperationEntry entry)
    {
        _operations.Insert(0, entry);
        if (_operations.Count > 20)
        {
            _operations.RemoveAt(_operations.Count - 1);
        }
    }

    public void RecordChallenge(bool isCorrect, int score)
    {
        ChallengesCompleted++;
        if (isCorrect)
        {
            CorrectChallenges++;
        }

        TotalScore += score;
    }

    public void RecordShift(int totalScore)
    {
        ShiftReportsCompleted++;
        TotalScore += totalScore;
    }

    public void Unlock(string code, string title, string description)
    {
        if (_achievements.Any(item => item.Code == code))
        {
            return;
        }

        _achievements.Add(new AchievementEntry(code, title, description, DateTimeOffset.UtcNow));
    }
}

public sealed record CryptoOperationEntry(Guid OperationId, string CipherCode, string Mode, string Input, string Output, DateTimeOffset ProcessedAt);

public sealed record AchievementEntry(string Code, string Title, string Description, DateTimeOffset UnlockedAt);
