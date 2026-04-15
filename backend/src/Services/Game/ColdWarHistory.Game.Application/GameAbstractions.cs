using ColdWarHistory.BuildingBlocks.Application;
using ColdWarHistory.BuildingBlocks.Contracts;
using ColdWarHistory.Game.Domain;

namespace ColdWarHistory.Game.Application;

public interface IGameRepository
{
    Task StoreChallengeAsync(TrainingChallenge challenge, CancellationToken cancellationToken);
    Task<TrainingChallenge?> GetChallengeAsync(Guid challengeId, CancellationToken cancellationToken);
    Task StoreShiftAsync(ShiftSession session, CancellationToken cancellationToken);
    Task<ShiftSession?> GetShiftAsync(Guid shiftId, CancellationToken cancellationToken);
    Task<DailyChallengeSnapshot?> GetDailyChallengeAsync(CancellationToken cancellationToken);
    Task SetDailyChallengeAsync(DailyChallengeSnapshot snapshot, CancellationToken cancellationToken);
}

public interface ICryptoGameClient
{
    Task<string> ExecuteAsync(string cipherCode, string mode, string input, IReadOnlyDictionary<string, string> parameters, CancellationToken cancellationToken);
}

public interface IGameProgressPublisher
{
    Task PublishChallengeCompletedAsync(ChallengeCompletedEvent integrationEvent, CancellationToken cancellationToken);
    Task PublishShiftCompletedAsync(ShiftCompletedEvent integrationEvent, CancellationToken cancellationToken);
}

public interface IGameService
{
    Task<TrainingChallengeDto> GenerateTrainingChallengeAsync(string cipherCode, string difficulty, CancellationToken cancellationToken);
    Task<ChallengeAttemptResultDto> SubmitChallengeAsync(Guid challengeId, SubmitTrainingAnswerRequest request, CurrentUser currentUser, CancellationToken cancellationToken);
    Task<DailyChallengeDto> GetDailyChallengeAsync(CancellationToken cancellationToken);
    Task RefreshDailyChallengeAsync(CancellationToken cancellationToken);
    Task<ShiftSessionDto> StartShiftAsync(string difficulty, CancellationToken cancellationToken);
    Task<ShiftResolutionDto> ResolveShiftMessageAsync(Guid shiftId, ResolveShiftMessageRequest request, CurrentUser currentUser, CancellationToken cancellationToken);
    Task<ShiftReportDto?> GetShiftReportAsync(Guid shiftId, CurrentUser currentUser, CancellationToken cancellationToken);
}
