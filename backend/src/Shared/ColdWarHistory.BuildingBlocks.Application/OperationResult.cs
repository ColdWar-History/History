namespace ColdWarHistory.BuildingBlocks.Application;

public class OperationResult
{
    protected OperationResult(bool isSuccess, OperationError? error)
    {
        IsSuccess = isSuccess;
        Error = error;
    }

    public bool IsSuccess { get; }

    public OperationError? Error { get; }

    public static OperationResult Success() => new(true, null);

    public static OperationResult Failure(OperationError error) => new(false, error);
}

public sealed class OperationResult<T> : OperationResult
{
    private OperationResult(bool isSuccess, T? value, OperationError? error) : base(isSuccess, error)
    {
        Value = value;
    }

    public T? Value { get; }

    public static OperationResult<T> Success(T value) => new(true, value, null);

    public new static OperationResult<T> Failure(OperationError error) => new(false, default, error);
}
