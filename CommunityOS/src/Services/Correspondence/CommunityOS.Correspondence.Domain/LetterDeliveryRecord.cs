namespace CommunityOS.Correspondence.Domain;

/// <summary>
/// A manual dispatch/delivery-outcome recording (ADR-028 decision 17). At the
/// first gate every dispatch is recorded with method code <c>manual</c>; the
/// future provider seam will add methods without changing this fact table.
/// </summary>
public sealed class LetterDeliveryRecord
{
    private LetterDeliveryRecord()
    {
    }

    public Guid Id { get; private set; }

    public Guid LetterId { get; private set; }

    public string MethodCode { get; private set; } = null!;

    public DeliveryOutcome Outcome { get; private set; }

    public string? ReasonCode { get; private set; }

    public Guid ActorId { get; private set; }

    public DateTime OccurredOn { get; private set; }

    public static LetterDeliveryRecord Create(
        Guid letterId, string methodCode, DeliveryOutcome outcome,
        string? reasonCode, Guid actorId, DateTime occurredOn)
    {
        if (string.IsNullOrWhiteSpace(methodCode))
        {
            throw new ArgumentException("Method code is required.", nameof(methodCode));
        }

        if (actorId == Guid.Empty)
        {
            throw new ArgumentException("Actor is required.", nameof(actorId));
        }

        return new LetterDeliveryRecord
        {
            Id = Guid.NewGuid(),
            LetterId = letterId,
            MethodCode = methodCode.Trim(),
            Outcome = outcome,
            ReasonCode = reasonCode,
            ActorId = actorId,
            OccurredOn = occurredOn
        };
    }
}
