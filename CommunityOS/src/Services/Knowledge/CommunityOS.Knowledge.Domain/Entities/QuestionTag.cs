using CommunityOS.SharedKernel.Domain.Primitives;
using CommunityOS.SharedKernel.Guards;

namespace CommunityOS.Knowledge.Domain.Entities;

/// <summary>
/// A user-assigned free-form tag attached to a question.
/// </summary>
public sealed class QuestionTag : Entity<Guid>
{
    private QuestionTag() : base(Guid.Empty)
    {
        Name = null!;
    }

    private QuestionTag(Guid id, string name) : base(id)
    {
        Name = name;
    }

    public string Name { get; private set; }

    public static QuestionTag Create(string name) =>
        new(Guid.NewGuid(), Guard.NotNullOrWhiteSpace(name, nameof(name)).Trim());
}