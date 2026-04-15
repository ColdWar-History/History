using System.Collections.Concurrent;
using System.Net.Http.Json;
using ColdWarHistory.BuildingBlocks.Application;
using ColdWarHistory.BuildingBlocks.Contracts;
using ColdWarHistory.Game.Application;
using ColdWarHistory.Game.Domain;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace ColdWarHistory.Game.Infrastructure;

public static class GameInfrastructureExtensions
{
    public static IServiceCollection AddGameInfrastructure(this IServiceCollection services)
    {
        services.AddSingleton<IGameRepository, InMemoryGameRepository>();
        services.AddHttpClient<ICryptoGameClient, CryptoGameClient>(client => client.BaseAddress = new Uri(ServiceEndpoints.Crypto));
        services.AddHttpClient<IGameProgressPublisher, GameProgressPublisher>(client => client.BaseAddress = new Uri(ServiceEndpoints.Progress));
        services.AddSingleton<IGameService, GameService>();
        services.AddHostedService<DailyChallengeRefreshWorker>();
        return services;
    }
}

internal sealed class InMemoryGameRepository : IGameRepository
{
    private readonly ConcurrentDictionary<Guid, TrainingChallenge> _challenges = new();
    private readonly ConcurrentDictionary<Guid, ShiftSession> _shifts = new();
    private DailyChallengeSnapshot? _daily;

    public Task StoreChallengeAsync(TrainingChallenge challenge, CancellationToken cancellationToken)
    {
        _challenges[challenge.Id] = challenge;
        return Task.CompletedTask;
    }

    public Task<TrainingChallenge?> GetChallengeAsync(Guid challengeId, CancellationToken cancellationToken) =>
        Task.FromResult(_challenges.TryGetValue(challengeId, out var challenge) ? challenge : null);

    public Task StoreShiftAsync(ShiftSession session, CancellationToken cancellationToken)
    {
        _shifts[session.Id] = session;
        return Task.CompletedTask;
    }

    public Task<ShiftSession?> GetShiftAsync(Guid shiftId, CancellationToken cancellationToken) =>
        Task.FromResult(_shifts.TryGetValue(shiftId, out var session) ? session : null);

    public Task<DailyChallengeSnapshot?> GetDailyChallengeAsync(CancellationToken cancellationToken) => Task.FromResult(_daily);

    public Task SetDailyChallengeAsync(DailyChallengeSnapshot snapshot, CancellationToken cancellationToken)
    {
        _daily = snapshot;
        return Task.CompletedTask;
    }
}

internal sealed class CryptoGameClient(HttpClient httpClient) : ICryptoGameClient
{
    public async Task<string> ExecuteAsync(string cipherCode, string mode, string input, IReadOnlyDictionary<string, string> parameters, CancellationToken cancellationToken)
    {
        var response = await httpClient.PostAsJsonAsync(
            "/api/crypto/transform",
            new CryptoTransformRequest(cipherCode, mode, input, new Dictionary<string, string>(parameters)),
            cancellationToken);

        response.EnsureSuccessStatusCode();
        var payload = await response.Content.ReadFromJsonAsync<CryptoTransformResponse>(cancellationToken);
        return payload?.Output ?? throw new InvalidOperationException("Crypto service returned empty response.");
    }
}

internal sealed class GameProgressPublisher(HttpClient httpClient) : IGameProgressPublisher
{
    public async Task PublishChallengeCompletedAsync(ChallengeCompletedEvent integrationEvent, CancellationToken cancellationToken)
    {
        await httpClient.PostAsJsonAsync("/internal/events/challenge-completed", integrationEvent, cancellationToken);
    }

    public async Task PublishShiftCompletedAsync(ShiftCompletedEvent integrationEvent, CancellationToken cancellationToken)
    {
        await httpClient.PostAsJsonAsync("/internal/events/shift-completed", integrationEvent, cancellationToken);
    }
}

internal sealed class DailyChallengeRefreshWorker(IServiceProvider serviceProvider) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromMinutes(1));

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = serviceProvider.CreateScope();
                var service = scope.ServiceProvider.GetRequiredService<IGameService>();
                await service.RefreshDailyChallengeAsync(stoppingToken);
            }
            catch
            {
                // Ignore transient startup failures; the next tick will retry.
            }

            if (!await timer.WaitForNextTickAsync(stoppingToken))
            {
                break;
            }
        }
    }
}
