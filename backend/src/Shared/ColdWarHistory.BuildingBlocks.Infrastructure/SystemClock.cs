using ColdWarHistory.BuildingBlocks.Application;

namespace ColdWarHistory.BuildingBlocks.Infrastructure;

public sealed class SystemClock : IClock
{
    public DateTimeOffset UtcNow => DateTimeOffset.UtcNow;
}
