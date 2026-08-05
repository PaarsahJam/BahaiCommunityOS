namespace CommunityOS.SharedKernel.Domain.Primitives;

public abstract class AggregateRoot<TId> : Entity<TId>
    where TId : notnull
{
    protected AggregateRoot(TId id) : base(id) { }

    public uint Version { get; protected set; }
}
