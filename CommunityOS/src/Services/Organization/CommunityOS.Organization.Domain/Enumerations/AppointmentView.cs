namespace CommunityOS.Organization.Domain.Enumerations;

/// <summary>
/// A temporal perspective for listing appointments. <see cref="Current"/>
/// returns appointments effective at the queried moment; the other views expose
/// the effective-dated history required by the Organization service (past,
/// upcoming and explicitly ended appointments).
/// </summary>
public enum AppointmentView
{
    /// <summary>Appointments effective at the queried moment (default).</summary>
    Current = 0,

    /// <summary>Appointments whose effective window has closed before the queried moment.</summary>
    Historical = 1,

    /// <summary>Appointments whose effective window starts after the queried moment.</summary>
    Upcoming = 2,

    /// <summary>Appointments explicitly ended (status Ended).</summary>
    Ended = 3,

    /// <summary>All appointments regardless of time or status.</summary>
    All = 4
}
