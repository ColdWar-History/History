namespace ColdWarHistory.BuildingBlocks.Application;

public interface IClock
{
    DateTimeOffset UtcNow { get; }
}
