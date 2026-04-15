namespace ColdWarHistory.BuildingBlocks.Domain;

public abstract class Entity<TId>
{
    protected Entity(TId id)
    {
        Id = id;
    }

    public TId Id { get; }
}
