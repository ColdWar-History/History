using ColdWarHistory.BuildingBlocks.Contracts;
using ColdWarHistory.Progress.Domain;

namespace ColdWarHistory.Progress.Application;

public interface IProgressRepository
{
    Task<UserProgressAggregate> GetOrCreateAsync(Guid userId, string userName, CancellationToken cancellationToken);
    Task<IReadOnlyCollection<UserProgressAggregate>> GetAllAsync(CancellationToken cancellationToken);
    Task SaveChangesAsync(CancellationToken cancellationToken);
}

public interface IProgressService
{
    Task RecordCryptoOperationAsync(CryptoOperationRecordedEvent integrationEvent, CancellationToken cancellationToken);
    Task RecordChallengeCompletedAsync(ChallengeCompletedEvent integrationEvent, CancellationToken cancellationToken);
    Task RecordShiftCompletedAsync(ShiftCompletedEvent integrationEvent, CancellationToken cancellationToken);
    Task<UserProfileDto> GetProfileAsync(Guid userId, string userName, CancellationToken cancellationToken);
    Task<IReadOnlyCollection<LeaderboardEntryDto>> GetLeaderboardAsync(CancellationToken cancellationToken);
}
