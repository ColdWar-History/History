using ColdWarHistory.BuildingBlocks.Application;
using ColdWarHistory.BuildingBlocks.Contracts;

namespace ColdWarHistory.Crypto.Application;

public interface ICryptoCatalog
{
    IReadOnlyCollection<CipherCatalogItem> GetAll();
}

public interface ICryptoOperationsService
{
    Task<OperationResult<CryptoTransformResponse>> ExecuteAsync(CryptoTransformRequest request, CurrentUser currentUser, CancellationToken cancellationToken);
}

public interface IProgressEventPublisher
{
    Task PublishCryptoOperationAsync(CryptoOperationRecordedEvent integrationEvent, CancellationToken cancellationToken);
}
