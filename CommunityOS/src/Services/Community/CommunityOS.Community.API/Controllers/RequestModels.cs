namespace CommunityOS.Community.API.Controllers;

public sealed record RescheduleRequest(DateTime StartsAt, DateTime? EndsAt);