using System.Net.Http.Json;
using ColdWarHistory.BuildingBlocks.Contracts;
using ColdWarHistory.Crypto.Application;
using ColdWarHistory.Crypto.Domain;
using Microsoft.Extensions.DependencyInjection;

namespace ColdWarHistory.Crypto.Infrastructure;

public static class CryptoInfrastructureExtensions
{
    public static IServiceCollection AddCryptoInfrastructure(this IServiceCollection services)
    {
        services.AddSingleton<ICipherAlgorithm, CaesarCipher>();
        services.AddSingleton<ICipherAlgorithm, AtbashCipher>();
        services.AddSingleton<ICipherAlgorithm, VigenereCipher>();
        services.AddSingleton<ICipherAlgorithm, RailFenceCipher>();
        services.AddSingleton<ICipherAlgorithm, ColumnarTranspositionCipher>();
        services.AddSingleton<ICryptoOperationsService, CryptoOperationsService>();
        services.AddSingleton<ICryptoCatalog>(sp => (ICryptoCatalog)sp.GetRequiredService<ICryptoOperationsService>());
        services.AddHttpClient<IProgressEventPublisher, ProgressEventPublisher>(client => client.BaseAddress = new Uri(ServiceEndpoints.Progress));
        return services;
    }
}

internal sealed class ProgressEventPublisher(HttpClient httpClient) : IProgressEventPublisher
{
    public async Task PublishCryptoOperationAsync(CryptoOperationRecordedEvent integrationEvent, CancellationToken cancellationToken)
    {
        await httpClient.PostAsJsonAsync("/internal/events/crypto-operation-recorded", integrationEvent, cancellationToken);
    }
}
