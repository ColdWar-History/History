using ColdWarHistory.BuildingBlocks.Contracts;
using ColdWarHistory.Progress.Domain;

namespace ColdWarHistory.Progress.Application;

public sealed class ProgressService(IProgressRepository repository) : IProgressService
{
    public async Task RecordCryptoOperationAsync(CryptoOperationRecordedEvent integrationEvent, CancellationToken cancellationToken)
    {
        var aggregate = await repository.GetOrCreateAsync(integrationEvent.UserId, integrationEvent.UserName, cancellationToken);
        aggregate.RecordOperation(new CryptoOperationEntry(
            integrationEvent.OperationId,
            integrationEvent.CipherCode,
            integrationEvent.Mode,
            integrationEvent.Input,
            integrationEvent.Output,
            integrationEvent.ProcessedAt));

        if (aggregate.Operations.Count >= 1)
        {
            aggregate.Unlock("first-signal", "Первый сигнал", "Выполнена первая криптооперация.");
        }

        await repository.SaveChangesAsync(cancellationToken);
    }

    public async Task RecordChallengeCompletedAsync(ChallengeCompletedEvent integrationEvent, CancellationToken cancellationToken)
    {
        var aggregate = await repository.GetOrCreateAsync(integrationEvent.UserId, integrationEvent.UserName, cancellationToken);
        aggregate.RecordChallenge(integrationEvent.IsCorrect, integrationEvent.Score);

        if (aggregate.ChallengesCompleted >= 1)
        {
            aggregate.Unlock("cadet", "Кадет криптошколы", "Завершено первое тренировочное задание.");
        }

        if (aggregate.CorrectChallenges >= 5)
        {
            aggregate.Unlock("analyst", "Аналитик", "Пять правильных тренировочных ответов.");
        }

        await repository.SaveChangesAsync(cancellationToken);
    }

    public async Task RecordShiftCompletedAsync(ShiftCompletedEvent integrationEvent, CancellationToken cancellationToken)
    {
        var aggregate = await repository.GetOrCreateAsync(integrationEvent.UserId, integrationEvent.UserName, cancellationToken);
        aggregate.RecordShift(integrationEvent.TotalScore);
        aggregate.Unlock("inspector", "Инспектор связи", "Завершена первая игровая смена.");
        await repository.SaveChangesAsync(cancellationToken);
    }

    public async Task<UserProfileDto> GetProfileAsync(Guid userId, string userName, CancellationToken cancellationToken)
    {
        var aggregate = await repository.GetOrCreateAsync(userId, userName, cancellationToken);
        return MapProfile(aggregate);
    }

    public async Task<IReadOnlyCollection<LeaderboardEntryDto>> GetLeaderboardAsync(CancellationToken cancellationToken)
    {
        var items = await repository.GetAllAsync(cancellationToken);
        return items
            .OrderByDescending(item => item.TotalScore)
            .ThenByDescending(item => item.CorrectChallenges)
            .Select((item, index) => new LeaderboardEntryDto(index + 1, item.Id, item.UserName, item.TotalScore, item.CorrectChallenges))
            .ToArray();
    }

    private static UserProfileDto MapProfile(UserProgressAggregate aggregate) =>
        new(
            aggregate.Id,
            aggregate.UserName,
            aggregate.Operations.Select(entry => new CryptoOperationHistoryDto(entry.OperationId, entry.CipherCode, entry.Mode, entry.Input, entry.Output, entry.ProcessedAt)).ToArray(),
            aggregate.Achievements.Select(item => new AchievementDto(item.Code, item.Title, item.Description, item.UnlockedAt)).ToArray(),
            new UserMetricsDto(aggregate.TotalScore, aggregate.ChallengesCompleted, aggregate.CorrectChallenges, aggregate.ShiftReportsCompleted, aggregate.Operations.Count));
}
