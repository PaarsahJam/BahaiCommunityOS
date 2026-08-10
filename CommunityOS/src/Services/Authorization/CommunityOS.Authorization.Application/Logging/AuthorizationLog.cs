using Microsoft.Extensions.Logging;

namespace CommunityOS.Authorization.Application.Logging;

public static partial class AuthorizationLog
{
    [LoggerMessage(
        EventId = 1,
        Level = LogLevel.Debug,
        Message = "Authorization decision {DecisionId} for permission {Permission}: allowed={Allowed}.")]
    public static partial void DecisionEmitted(this ILogger logger, string decisionId, string permission, bool allowed);

    [LoggerMessage(
        EventId = 2,
        Level = LogLevel.Warning,
        Message = "Dangling role reference {RoleId} on assignment {AssignmentId} ignored (fail closed).")]
    public static partial void DanglingRole(this ILogger logger, Guid roleId, Guid assignmentId);
}
