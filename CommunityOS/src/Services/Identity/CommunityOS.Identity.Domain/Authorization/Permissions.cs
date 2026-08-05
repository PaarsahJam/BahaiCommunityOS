namespace CommunityOS.Identity.Domain.Authorization;

public static class Permissions
{
    public static class Members
    {
        public const string View     = "Members.View";
        public const string Create   = "Members.Create";
        public const string Edit     = "Members.Edit";
        public const string Activate = "Members.Activate";
        public const string Suspend  = "Members.Suspend";
        public const string Transfer = "Members.Transfer";
        public const string Delete   = "Members.Delete";
    }

    public static class Roles
    {
        public const string View   = "Roles.View";
        public const string Assign = "Roles.Assign";
        public const string Manage = "Roles.Manage";
    }

    public static class Community
    {
        public const string View   = "Community.View";
        public const string Manage = "Community.Manage";
    }

    public static class Events
    {
        public const string View   = "Events.View";
        public const string Create = "Events.Create";
        public const string Manage = "Events.Manage";
    }

    public static class Content
    {
        public const string View    = "Content.View";
        public const string Publish = "Content.Publish";
        public const string Manage  = "Content.Manage";
    }

    public static class Enrollment
    {
        public const string View   = "Enrollment.View";
        public const string Enroll = "Enrollment.Enroll";
        public const string Manage = "Enrollment.Manage";
    }

    public static class Reports
    {
        public const string View     = "Reports.View";
        public const string Generate = "Reports.Generate";
    }
}
