using ColdWarHistory.BuildingBlocks.Application;
using ColdWarHistory.BuildingBlocks.Contracts;
using ColdWarHistory.Crypto.Domain;

namespace ColdWarHistory.Crypto.Application;

public sealed class CryptoOperationsService(
    IEnumerable<ICipherAlgorithm> algorithms,
    IClock clock,
    IProgressEventPublisher progressEventPublisher) : ICryptoOperationsService, ICryptoCatalog
{
    private readonly IReadOnlyDictionary<string, ICipherAlgorithm> _algorithms = algorithms.ToDictionary(item => item.Code, StringComparer.OrdinalIgnoreCase);

    public IReadOnlyCollection<CipherCatalogItem> GetAll() =>
        _algorithms.Values
            .OrderBy(item => item.Difficulty)
            .Select(item => new CipherCatalogItem(
                item.Code,
                item.Name,
                item.Category,
                item.Era,
                item.Difficulty,
                item.Parameters.Select(parameter => new CipherParameterDefinition(parameter.Name, parameter.Label, parameter.Type, parameter.IsRequired, parameter.Description)).ToArray()))
            .ToArray();

    public async Task<OperationResult<CryptoTransformResponse>> ExecuteAsync(CryptoTransformRequest request, CurrentUser currentUser, CancellationToken cancellationToken)
    {
        if (!_algorithms.TryGetValue(request.CipherCode, out var algorithm))
        {
            return OperationResult<CryptoTransformResponse>.Failure(OperationError.NotFound($"Cipher '{request.CipherCode}' is not supported."));
        }

        try
        {
            var executionResult = string.Equals(request.Mode, "decrypt", StringComparison.OrdinalIgnoreCase)
                ? algorithm.Decrypt(request.Input, request.Parameters)
                : algorithm.Encrypt(request.Input, request.Parameters);

            Guid? operationId = null;
            if (currentUser.IsAuthenticated)
            {
                operationId = Guid.NewGuid();
                await progressEventPublisher.PublishCryptoOperationAsync(
                    new CryptoOperationRecordedEvent(
                        operationId.Value,
                        currentUser.UserId!.Value,
                        currentUser.UserName ?? "unknown",
                        request.CipherCode,
                        request.Mode,
                        request.Input,
                        executionResult.Output,
                        clock.UtcNow),
                    cancellationToken);
            }

            return OperationResult<CryptoTransformResponse>.Success(
                new CryptoTransformResponse(
                    request.CipherCode,
                    request.Mode,
                    request.Input,
                    executionResult.Output,
                    executionResult.Steps.Select(step => new CryptoStepDto(step.Order, step.Title, step.Description, step.Snapshot)).ToArray(),
                    executionResult.ValidationMessages.ToArray(),
                    clock.UtcNow,
                    operationId));
        }
        catch (InvalidOperationException exception)
        {
            return OperationResult<CryptoTransformResponse>.Failure(OperationError.Validation(exception.Message));
        }
    }
}
