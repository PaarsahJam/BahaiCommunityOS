namespace CommunityOS.Identity.Application.DTOs;

public sealed record TokenDto(string AccessToken, string RefreshToken, DateTime ExpiresAt);

public sealed record LoginResponseDto(
    Guid UserAccountId,
    string Email,
    bool RequiresMfa,
    TokenDto? Tokens);

public sealed record MfaEnrollmentDto(
    Guid MfaMethodId,
    string Secret,
    string ProvisioningUri);

public sealed record VerificationTokenDto(string Token, DateTime ExpiresAt);

public sealed record UserAccountDto(
    Guid Id,
    string Email,
    string Status,
    DateTime CreatedOn,
    DateTime? VerifiedOn,
    DateTime? LastLoginOn,
    IReadOnlyList<ExternalIdentityDto> ExternalIdentities,
    IReadOnlyList<MfaMethodDto> MfaMethods,
    IReadOnlyList<DeviceDto> Devices);

public sealed record ExternalIdentityDto(Guid Id, string Provider, string Subject, DateTime LinkedOn);

public sealed record MfaMethodDto(Guid Id, string Type, bool IsVerified, bool IsActive, DateTime CreatedOn);

public sealed record DeviceDto(Guid Id, string Name, string? Platform, DateTime RegisteredOn, bool IsTrusted);

public sealed record SessionDto(
    Guid Id,
    Guid DeviceId,
    string? DeviceName,
    string? DevicePlatform,
    DateTime CreatedOn,
    DateTime ExpiresOn,
    DateTime LastUsedOn,
    bool IsActive);

public sealed record SecurityEventDto(Guid Id, string EventType, string? Description, DateTime OccurredOn);

public sealed record RecoveryTokenDto(string Token, DateTime ExpiresAt);

public sealed record AuthorizationCodeDto(string Code, string? State, DateTime ExpiresAt);

public sealed record TokenResponseDto(
    string AccessToken,
    string TokenType,
    int ExpiresIn,
    string? RefreshToken,
    string? Scope,
    string? IdToken);
