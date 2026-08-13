using CommunityOS.SharedKernel.Domain.Primitives;
using CommunityOS.SharedKernel.Guards;

namespace CommunityOS.Knowledge.Domain.Entities;

/// <summary>
/// A comment within a discussion thread.
/// </summary>
public sealed class Comment : Entity<Guid>
{
    private Comment() : base(Guid.Empty)
    {
        Body = null!;
    }

    private Comment(Guid id, Guid authorId, string body, DateTime createdOn) : base(id)
    {
        AuthorId = authorId;
        Body = body;
        CreatedOn = createdOn;
    }

    public Guid AuthorId { get; private set; }
    public string Body { get; private set; }
    public DateTime CreatedOn { get; private set; }

    public static Comment Create(Guid authorId, string body, DateTime createdOn)
    {
        Guard.NotDefault(authorId, nameof(authorId));
        Guard.NotNullOrWhiteSpace(body, nameof(body));
        Guard.MaxLength(body, 10000, nameof(body));

        return new Comment(Guid.NewGuid(), authorId, body.Trim(), createdOn);
    }
}