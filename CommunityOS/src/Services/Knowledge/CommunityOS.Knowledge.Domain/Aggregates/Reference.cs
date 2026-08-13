using CommunityOS.Knowledge.Domain.Enumerations;
using CommunityOS.SharedKernel.Domain.Primitives;
using CommunityOS.SharedKernel.Guards;

namespace CommunityOS.Knowledge.Domain.Aggregates;

/// <summary>
/// A reference from community content (question, answer, comment, discussion)
/// to a Library passage. Reference validation happens in the application layer
/// because it requires cross-aggregate reads (passage exists and its edition is
/// verified); the domain root enforces shape and integrity invariants only.
/// </summary>
public sealed class Reference : AggregateRoot<Guid>
{
    private Reference() : base(Guid.Empty)
    {
        OwnerType = null!;
    }

    private Reference(
        Guid id,
        ReferenceOwnerType ownerType,
        Guid ownerId,
        Guid passageId,
        Guid editionId) : base(id)
    {
        OwnerType = ownerType;
        OwnerId = ownerId;
        PassageId = passageId;
        EditionId = editionId;
    }

    public ReferenceOwnerType OwnerType { get; private set; }
    public Guid OwnerId { get; private set; }
    public Guid PassageId { get; private set; }
    public Guid EditionId { get; private set; }

    public static Reference Create(
        ReferenceOwnerType ownerType,
        Guid ownerId,
        Guid passageId,
        Guid editionId)
    {
        Guard.NotNull(ownerType, nameof(ownerType));
        Guard.NotDefault(ownerId, nameof(ownerId));
        Guard.NotDefault(passageId, nameof(passageId));
        Guard.NotDefault(editionId, nameof(editionId));

        return new Reference(Guid.NewGuid(), ownerType, ownerId, passageId, editionId);
    }
}