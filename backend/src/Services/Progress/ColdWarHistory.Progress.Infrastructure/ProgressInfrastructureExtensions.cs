using System.Collections.Concurrent;
using ColdWarHistory.Progress.Application;
using ColdWarHistory.Progress.Domain;
using Microsoft.Extensions.DependencyInjection;

namespace ColdWarHistory.Progress.Infrastructure;

public static class ProgressInfrastructureExtensions
{
    public static IServiceCollection AddProgressInfrastructure(this IServiceCollection services)
    {
        services.AddSingleton<IProgressRepository, InMemoryProgressRepository>();
        services.AddSingleton<IProgressService, ProgressService>();
        return services;
    }
}

internal sealed class InMemoryProgressRepository : IProgressRepository
{
    private readonly ConcurrentDictionary<Guid, UserProgressAggregate> _items = new();

    public Task<UserProgressAggregate> GetOrCreateAsync(Guid userId, string userName, CancellationToken cancellationToken)
    {
        var aggregate = _items.GetOrAdd(userId, id => new UserProgressAggregate(id, userName));
        aggregate.Rename(userName);
        return Task.FromResult(aggregate);
    }

    public Task<IReadOnlyCollection<UserProgressAggregate>> GetAllAsync(CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyCollection<UserProgressAggregate>>(_items.Values.ToArray());

    public Task SaveChangesAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
