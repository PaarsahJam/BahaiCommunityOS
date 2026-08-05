namespace CommunityOS.SharedKernel.Domain.Events;

public abstract record DomainEvent : IDomainEvent
{
    protected DomainEvent()
    {
        EventId = Guid.NewGuid();
        OccurredOn = DateTime.UtcNow;
    }

    public Guid EventId { get; init; }
    public DateTime OccurredOn { get; init; }
}
