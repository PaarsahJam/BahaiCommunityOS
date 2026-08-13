using CommunityOS.Knowledge.Domain.Events;
using CommunityOS.SharedKernel.Domain.Primitives;
using CommunityOS.SharedKernel.Guards;

namespace CommunityOS.Knowledge.Domain.Aggregates;

/// <summary>
/// A controlled category that organizes questions and answers. Taxonomy is a
/// global concern (not org-scoped) and is governed by the Library boundary.
/// </summary>
public sealed class Category : AggregateRoot<Guid>
{
    private Category() : base(Guid.Empty)
    {
        Name = null!;
    }

    private Category(Guid id, string name, string? description) : base(id)
    {
        Name = name;
        Description = description;
    }

    public string Name { get; private set; }
    public string? Description { get; private set; }

    public static Category Create(string name, string? description)
    {
        Guard.NotNullOrWhiteSpace(name, nameof(name));
        Guard.MaxLength(name, 200, nameof(name));
        Guard.MaxLength(description ?? string.Empty, 1000, nameof(description));

        var category = new Category(Guid.NewGuid(), name.Trim(), TrimBlank(description));
        category.RaiseDomainEvent(new CategoryCreatedEvent(category.Id, category.Name));
        return category;
    }

    public void Update(string name, string? description)
    {
        Guard.NotNullOrWhiteSpace(name, nameof(name));
        Guard.MaxLength(name, 200, nameof(name));
        Guard.MaxLength(description ?? string.Empty, 1000, nameof(description));

        Name = name.Trim();
        Description = TrimBlank(description);

        RaiseDomainEvent(new CategoryUpdatedEvent(Id, Name));
    }

    private static string? TrimBlank(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}