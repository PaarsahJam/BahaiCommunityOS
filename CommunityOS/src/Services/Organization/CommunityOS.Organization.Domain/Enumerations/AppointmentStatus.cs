using CommunityOS.SharedKernel.Domain.Primitives;

namespace CommunityOS.Organization.Domain.Enumerations;

/// <summary>
/// Lifecycle state of an appointment.
/// </summary>
public sealed class AppointmentStatus : Enumeration<int>
{
    public static readonly AppointmentStatus Active  = new(1, "Active");
    public static readonly AppointmentStatus Ended   = new(2, "Ended");

    private AppointmentStatus(int id, string name) : base(id, name) { }

    public static IEnumerable<AppointmentStatus> All => [Active, Ended];

    public static AppointmentStatus FromName(string name) =>
        All.FirstOrDefault(s => string.Equals(s.Name, name, StringComparison.OrdinalIgnoreCase))
        ?? throw new ArgumentException($"Unknown AppointmentStatus name: {name}");

    public static AppointmentStatus FromId(int id) =>
        All.FirstOrDefault(s => s.Id == id)
        ?? throw new ArgumentException($"Unknown AppointmentStatus id: {id}");
}
