namespace ColdWarHistory.BuildingBlocks.Application;

public sealed record OperationError(string Code, string Message)
{
    public static OperationError Validation(string message) => new("validation_error", message);
    public static OperationError NotFound(string message) => new("not_found", message);
    public static OperationError Unauthorized(string message) => new("unauthorized", message);
    public static OperationError Conflict(string message) => new("conflict", message);
}
