using CommunityOS.Knowledge.Domain.Events;
using CommunityOS.SharedKernel.Domain.Primitives;
using CommunityOS.SharedKernel.Guards;

namespace CommunityOS.Knowledge.Domain.Aggregates;

/// <summary>
/// A controlled topic that organizes questions and answers. Taxonomy is a
/// global concern (not org-scoped) and is governed by the Library boundary.
/// </summary>
public sealed class Topic : AggregateRoot<Guid>
{
    private Topic() : base(Guid.Empty)
    {
        Name = null!;
    }

    private Topic(Guid id, string name, string? description) : base(id)
    {
        Name = name;
        Description = description;
    }

    public string Name { get; private set; }
    public string? Description { get; private set; }

    public static Topic Create(string name, string? description)
    {
        Guard.NotNullOrWhiteSpace(name, nameof(name));
        Guard.MaxLength(name, 200, nameof(name));
        Guard.MaxLength(description ?? string.Empty, 1000, nameof(description));

        var topic = new Topic(Guid.NewGuid(), name.Trim(), TrimBlank(description));
        topic.RaiseDomainEvent(new TopicCreatedEvent(topic.Id, topic.Name));
        return topic;
    }

    public void Update(string name, string? description)
    {
        Guard.NotNullOrWhiteSpace(name, nameof(name));
        Guard.MaxLength(name, 200, nameof(name));
        Guard.MaxLength(description ?? string.Empty, 1000, nameof(description));

        Name = name.Trim();
        Description = TrimBlank(description);

        RaiseDomainEvent(new TopicUpdatedEvent(Id, Name));
    }

    private static string? TrimBlank(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}