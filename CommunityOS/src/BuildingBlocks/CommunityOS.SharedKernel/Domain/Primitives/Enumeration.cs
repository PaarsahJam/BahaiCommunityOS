namespace CommunityOS.SharedKernel.Domain.Primitives;

public abstract class Enumeration<TId>(TId id, string name) : IEquatable<Enumeration<TId>>
    where TId : notnull
{
    public TId Id { get; } = id;
    public string Name { get; } = name;

    public bool Equals(Enumeration<TId>? other) =>
        other is not null &&
        GetType() == other.GetType() &&
        EqualityComparer<TId>.Default.Equals(Id, other.Id);

    public override bool Equals(object? obj) =>
        obj is Enumeration<TId> e && Equals(e);

    public override int GetHashCode() => Id.GetHashCode();

    public override string ToString() => Name;
}
