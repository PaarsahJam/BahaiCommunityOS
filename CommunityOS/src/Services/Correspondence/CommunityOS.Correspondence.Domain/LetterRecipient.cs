using CommunityOS.Correspondence.Domain.Exceptions;

namespace CommunityOS.Correspondence.Domain;

/// <summary>
/// A recipient reference on a letter (ADR-028 decision 5). Person and unit
/// recipients are stable-id references into Community/Organization; external
/// recipients carry the immutable addressing snapshot as bounded display
/// lines. Kind exclusivity is enforced here and again by a database check
/// constraint.
/// </summary>
public sealed class LetterRecipient
{
    private LetterRecipient()
    {
    }

    public Guid Id { get; private set; }

    public Guid LetterId { get; private set; }

    public RecipientKind Kind { get; private set; }

    public Guid? PersonId { get; private set; }

    public Guid? UnitId { get; private set; }

    /// <summary>External addressing snapshot lines; never logged or published.</summary>
    public string? DisplayLine { get; private set; }

    public DateTime AddedOn { get; private set; }

    public static LetterRecipient Create(
        Guid letterId, RecipientKind kind, Guid? personId, Guid? unitId, string? displayLine, DateTime addedOn)
    {
        if (letterId == Guid.Empty)
        {
            throw new ArgumentException("Letter id is required.", nameof(letterId));
        }

        switch (kind)
        {
            case RecipientKind.Person when personId is not { } pid || pid == Guid.Empty:
                throw new ArgumentException("Person recipients require a person id.");
            case RecipientKind.Person when unitId is not null || !string.IsNullOrWhiteSpace(displayLine):
                throw new ArgumentException("Person recipients may not carry a unit id or display line.");

            case RecipientKind.Unit when unitId is not { } uid || uid == Guid.Empty:
                throw new ArgumentException("Unit recipients require a unit id.");
            case RecipientKind.Unit when personId is not null || !string.IsNullOrWhiteSpace(displayLine):
                throw new ArgumentException("Unit recipients may not carry a person id or display line.");

            case RecipientKind.External when string.IsNullOrWhiteSpace(displayLine):
                throw new ArgumentException("External recipients require an addressing display line.");
            case RecipientKind.External when displayLine!.Length > 500:
                throw new ArgumentException("External display lines are limited to 500 characters.");
            case RecipientKind.External when personId is not null || unitId is not null:
                throw new ArgumentException("External recipients may not carry person or unit ids.");

            default:
                break;
        }

        return new LetterRecipient
        {
            Id = Guid.NewGuid(),
            LetterId = letterId,
            Kind = kind,
            PersonId = kind == RecipientKind.Person ? personId : null,
            UnitId = kind == RecipientKind.Unit ? unitId : null,
            DisplayLine = kind == RecipientKind.External ? displayLine!.Trim() : null,
            AddedOn = addedOn
        };
    }

    /// <summary>Structural equality for duplicate-recipient detection.</summary>
    public bool Equals(LetterRecipient other) =>
        other is not null &&
        Kind == other.Kind &&
        PersonId == other.PersonId &&
        UnitId == other.UnitId &&
        string.Equals(DisplayLine, other.DisplayLine, StringComparison.Ordinal);
}
