using CommunityOS.SharedKernel.Domain.Primitives;
using CommunityOS.SharedKernel.Guards;

namespace CommunityOS.Identity.Domain.Entities;

public sealed class Permission : Entity<Guid>
{
    public string Code { get; private set; }
    public string Description { get; private set; }

    private Permission(Guid id, string code, string description) : base(id)
    {
        Code = code;
        Description = description;
    }

    public static Permission Create(string code, string description)
    {
        Guard.NotNullOrWhiteSpace(code, nameof(code));
        Guard.NotNullOrWhiteSpace(description, nameof(description));
        return new Permission(Guid.NewGuid(), code.ToUpperInvariant(), description.Trim());
    }
}
