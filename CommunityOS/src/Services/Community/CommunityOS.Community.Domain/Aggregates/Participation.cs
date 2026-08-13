using CommunityOS.Community.Domain.Enumerations;
using CommunityOS.Community.Domain.Events;
using CommunityOS.Community.Domain.Exceptions;
using CommunityOS.Community.Domain.ValueObjects;
using CommunityOS.SharedKernel.Domain.Primitives;
using CommunityOS.SharedKernel.Guards;

namespace CommunityOS.Community.Domain.Aggregates;

/// <summary>
/// A single community participation record. One abstraction covers activity,
/// event, meeting and volunteer/service participation; a volunteer assignment
/// is not an organization appointment unless it is explicitly created as one
/// in the Organization service.
/// </summary>
public sealed class Participation : AggregateRoot<Guid>
{
    private Participation() : base(Guid.Empty)
    {
        TargetType = null!;
        Status = null!;
        Period = null!;
    }

    private Participation(
        Guid id,
        Guid personId,
        ParticipationTargetType targetType,
        Guid targetId,
        string? role,
        ParticipationStatus status,
        EffectivePeriod period,
        DateTime recordedOn) : base(id)
    {
        PersonId = personId;
        TargetType = targetType;
        TargetId = targetId;
        Role = role;
        Status = status;
        Period = period;
        RecordedOn = recordedOn;
    }

    public Guid PersonId { get; private set; }
    public ParticipationTargetType TargetType { get; private set; }
    public Guid TargetId { get; private set; }
    public string? Role { get; private set; }
    public ParticipationStatus Status { get; private set; }
    public EffectivePeriod Period { get; private set; }
    public DateTime RecordedOn { get; private set; }

    public static Participation Create(
        Guid personId,
        ParticipationTargetType targetType,
        Guid targetId,
        string? role,
        ParticipationStatus status,
        EffectivePeriod period)
    {
        Guard.NotDefault(personId, nameof(personId));
        Guard.NotNull(targetType, nameof(targetType));
        Guard.NotDefault(targetId, nameof(targetId));
        Guard.MaxLength(role ?? string.Empty, 100, nameof(role));
        Guard.NotNull(status, nameof(status));
        Guard.NotNull(period, nameof(period));

        var participation = new Participation(
            Guid.NewGuid(),
            personId,
            targetType,
            targetId,
            TrimBlank(role),
            status,
            period,
            DateTime.UtcNow);

        participation.RaiseDomainEvent(new ParticipationRecordedEvent(
            participation.Id,
            participation.PersonId,
            participation.TargetType.Name,
            participation.TargetId,
            participation.Status.Name,
            Participation.OccurredAt()));
        return participation;
    }

    public void UpdateStatus(ParticipationStatus status)
    {
        Guard.NotNull(status, nameof(status));

        if (Status == ParticipationStatus.Cancelled)
            throw new ParticipationAlreadyCancelledException(Id);

        Status = status;
        RaiseDomainEvent(new ParticipationRecordedEvent(Id, PersonId, TargetType.Name, TargetId, Status.Name, OccurredAt()));
    }

    public void MarkAttended() => UpdateStatus(ParticipationStatus.Attended);

    public void Cancel()
    {
        if (Status == ParticipationStatus.Cancelled)
            throw new ParticipationAlreadyCancelledException(Id);

        Status = ParticipationStatus.Cancelled;
        RaiseDomainEvent(new ParticipationRecordedEvent(Id, PersonId, TargetType.Name, TargetId, Status.Name, OccurredAt()));
    }

    private static DateTime OccurredAt() => DateTime.UtcNow;

    private static string? TrimBlank(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
