using CommunityOS.Notifications.Domain.Exceptions;
using CommunityOS.Notifications.Domain.ValueObjects;

namespace CommunityOS.Notifications.Tests.Domain;

/// <summary>
/// Notification-type catalog behavior (ADR-025, decision 6): configuration, not
/// a hard enum; lowercased codes; retired types can never be updated; retire is
/// idempotent and never deletes.
/// </summary>
public class NotificationTypeTests
{
    private static readonly Guid Actor = Guid.NewGuid();
    private static readonly DateTime Now = DateTime.UtcNow;

    [Fact]
    public void Create_normalizes_code_to_lowercase()
    {
        var type = NotificationType.Create(
            "Task-Assigned", "Task assigned", NotificationChannel.InApp,
            "A task has been assigned to you", "Task {{TaskId}} assigned.", false, Actor, Now);

        type.Code.Should().Be("task-assigned");
        type.IsActive.Should().BeTrue();
    }

    [Fact]
    public void Retired_type_rejects_updates()
    {
        var type = NotificationType.Create(
            "task-assigned", "Task assigned", NotificationChannel.InApp,
            "subject", "body", false, Actor, Now);
        type.Retire(Actor, Now);

        var act = () => type.Update(
            "Renamed", NotificationChannel.InApp, "subject", "body", false, Actor, Now);

        act.Should().Throw<RetiredNotificationTypeUpdateException>();
    }

    [Fact]
    public void Retire_is_idempotent_and_never_deletes()
    {
        var type = NotificationType.Create(
            "task-assigned", "Task assigned", NotificationChannel.InApp,
            "subject", "body", false, Actor, Now);

        type.Retire(Actor, Now);
        type.Retire(Actor, Now);

        type.IsActive.Should().BeFalse();
        type.RetiredBy.Should().Be(Actor);
        type.RetiredOn.Should().NotBeNull();
    }

    [Fact]
    public void Update_changes_templates_and_flags()
    {
        var type = NotificationType.Create(
            "task-assigned", "Task assigned", NotificationChannel.InApp,
            "subject", "body", false, Actor, Now);

        type.Update("Task assigned (new)", NotificationChannel.Email,
            "new subject", "new body", isSensitive: true, Actor, Now);

        type.DisplayName.Should().Be("Task assigned (new)");
        type.DefaultChannel.Should().Be(NotificationChannel.Email);
        type.SubjectTemplate.Should().Be("new subject");
        type.BodyTemplate.Should().Be("new body");
        type.IsSensitive.Should().BeTrue();
        type.UpdatedBy.Should().Be(Actor);
    }
}

/// <summary>
/// The ratified channel catalog (ADR-025, decision 5): Email, Push, InApp, Sms;
/// lookup by id or name is case-insensitive; unknown channels throw.
/// </summary>
public class NotificationChannelTests
{
    [Fact]
    public void FromName_is_case_insensitive()
    {
        NotificationChannel.FromName("inapp").Should().Be(NotificationChannel.InApp);
        NotificationChannel.FromName("EMAIL").Should().Be(NotificationChannel.Email);
    }

    [Fact]
    public void FromId_round_trips_all_channels()
    {
        foreach (var channel in NotificationChannel.All)
            NotificationChannel.FromId(channel.Id).Should().Be(channel);
    }

    [Fact]
    public void Unknown_channel_throws()
    {
        var act = () => NotificationChannel.FromName("fax");

        act.Should().Throw<InvalidNotificationChannelException>();
    }
}