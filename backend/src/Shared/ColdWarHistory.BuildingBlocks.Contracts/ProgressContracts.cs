namespace ColdWarHistory.BuildingBlocks.Contracts;

public sealed record AchievementDto(string Code, string Title, string Description, DateTimeOffset? UnlockedAt);

public sealed record CryptoOperationHistoryDto(
    Guid OperationId,
    string CipherCode,
    string Mode,
    string Input,
    string Output,
    DateTimeOffset ProcessedAt);

public sealed record UserMetricsDto(
    int TotalScore,
    int ChallengesCompleted,
    int CorrectChallenges,
    int ShiftReportsCompleted,
    int CryptoOperations);

public sealed record UserProfileDto(
    Guid UserId,
    string UserName,
    IReadOnlyCollection<CryptoOperationHistoryDto> RecentOperations,
    IReadOnlyCollection<AchievementDto> Achievements,
    UserMetricsDto Metrics);

public sealed record LeaderboardEntryDto(int Rank, Guid UserId, string UserName, int Score, int CorrectChallenges);
