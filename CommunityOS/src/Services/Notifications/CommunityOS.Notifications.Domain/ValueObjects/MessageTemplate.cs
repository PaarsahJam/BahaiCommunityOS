using CommunityOS.SharedKernel.Domain.Primitives;
using CommunityOS.SharedKernel.Guards;

namespace CommunityOS.Notifications.Domain.ValueObjects;

public sealed class MessageTemplate : ValueObject
{
    public string Subject { get; }
    public string Body { get; }

    private MessageTemplate(string subject, string body)
    {
        Subject = subject;
        Body = body;
    }

    public static MessageTemplate Create(string subject, string body)
    {
        Guard.NotNullOrWhiteSpace(subject, nameof(subject));
        Guard.NotNullOrWhiteSpace(body, nameof(body));
        Guard.MaxLength(subject, 500, nameof(subject));
        return new MessageTemplate(subject.Trim(), body.Trim());
    }

    public string Render(IReadOnlyDictionary<string, string> variables)
    {
        var rendered = Body;
        foreach (var (key, value) in variables)
            rendered = rendered.Replace($"{{{{{key}}}}}", value);
        return rendered;
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Subject;
        yield return Body;
    }
}
