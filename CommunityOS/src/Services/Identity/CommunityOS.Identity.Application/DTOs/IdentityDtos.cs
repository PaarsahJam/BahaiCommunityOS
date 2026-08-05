namespace CommunityOS.Identity.Application.DTOs;

public sealed record MemberDto(
    Guid Id,
    string FirstName,
    string LastName,
    string Email,
    string? PhoneNumber,
    string Status,
    Guid? LocalUnitId,
    DateTime EnrolledOn,
    IReadOnlyList<RoleDto> Roles);

public sealed record RoleDto(
    Guid Id,
    string Name,
    bool IsSystemRole,
    IReadOnlyList<string> PermissionCodes);

public sealed record TokenDto(
    string AccessToken,
    string RefreshToken,
    DateTime ExpiresAt);
