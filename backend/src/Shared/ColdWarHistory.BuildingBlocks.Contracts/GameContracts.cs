namespace ColdWarHistory.BuildingBlocks.Contracts;

public sealed record TrainingChallengeDto(
    Guid Id,
    string CipherCode,
    string Difficulty,
    string Prompt,
    string Input,
    string ExpectedMode,
    IReadOnlyDictionary<string, string> Parameters,
    int BaseScore,
    DateTimeOffset GeneratedAt);

public sealed record SubmitTrainingAnswerRequest(string Answer, bool UsedHint);

public sealed record ChallengeAttemptResultDto(
    Guid ChallengeId,
    bool IsCorrect,
    int AwardedScore,
    string Explanation,
    string ExpectedAnswer,
    string UserAnswer,
    DateTimeOffset EvaluatedAt);

public sealed record ChallengeCompletedEvent(
    Guid ChallengeId,
    Guid UserId,
    string UserName,
    string CipherCode,
    bool IsCorrect,
    int Score,
    DateTimeOffset CompletedAt);

public sealed record DailyChallengeDto(
    DateOnly Date,
    TrainingChallengeDto Challenge,
    string Theme);

public sealed record StartShiftRequest(string Difficulty);

public sealed record ShiftMessageDto(Guid MessageId, string Headline, string EncodedMessage, string CipherCode, string Briefing);

public sealed record ShiftSessionDto(Guid ShiftId, string Difficulty, IReadOnlyCollection<ShiftMessageDto> Messages, DateTimeOffset StartedAt);

public sealed record ResolveShiftMessageRequest(Guid MessageId, string Decision, string? DecodedMessage);

public sealed record ShiftResolutionDto(
    Guid MessageId,
    string Decision,
    bool IsCorrect,
    int ScoreDelta,
    string Explanation);

public sealed record ShiftReportDto(
    Guid ShiftId,
    int TotalScore,
    int CorrectDecisions,
    int IncorrectDecisions,
    IReadOnlyCollection<ShiftResolutionDto> Resolutions,
    string Recommendation,
    DateTimeOffset CompletedAt);

public sealed record ShiftCompletedEvent(
    Guid ShiftId,
    Guid UserId,
    string UserName,
    int TotalScore,
    int CorrectDecisions,
    int IncorrectDecisions,
    DateTimeOffset CompletedAt);
