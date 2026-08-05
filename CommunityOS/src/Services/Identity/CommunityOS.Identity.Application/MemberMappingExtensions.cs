using CommunityOS.Identity.Application.DTOs;
using CommunityOS.Identity.Domain.Aggregates;

namespace CommunityOS.Identity.Application;

internal static class MemberMappingExtensions
{
    internal static MemberDto ToDto(this Member member) =>
        new(
            member.Id,
            member.Name.FirstName,
            member.Name.LastName,
            member.Email.Value,
            member.PhoneNumber?.Value,
            member.Status.Name,
            member.LocalUnitId,
            member.EnrolledOn,
            member.Roles
                .Select(r => new RoleDto(
                    r.Id,
                    r.Name,
                    r.IsSystemRole,
                    r.Permissions.Select(p => p.Code).ToList().AsReadOnly()))
                .ToList()
                .AsReadOnly());
}
