import 'package:freezed_annotation/freezed_annotation.dart';

part 'auth_dtos.freezed.dart';
part 'auth_dtos.g.dart';

@freezed
abstract class LoginRequestDto with _$LoginRequestDto {
  const factory LoginRequestDto({
    required String email,
    required String password,
    String? mfaCode,
    String? deviceName,
    String? platform,
    String? userAgent,
    String? ipAddress,
  }) = _LoginRequestDto;

  factory LoginRequestDto.fromJson(Map<String, dynamic> json) =>
      _$LoginRequestDtoFromJson(json);
}

@freezed
abstract class RefreshRequestDto with _$RefreshRequestDto {
  const factory RefreshRequestDto({
    required String refreshToken,
    String? ipAddress,
  }) = _RefreshRequestDto;

  factory RefreshRequestDto.fromJson(Map<String, dynamic> json) =>
      _$RefreshRequestDtoFromJson(json);
}

@freezed
abstract class LogoutRequestDto with _$LogoutRequestDto {
  const factory LogoutRequestDto({required String refreshToken}) =
      _LogoutRequestDto;

  factory LogoutRequestDto.fromJson(Map<String, dynamic> json) =>
      _$LogoutRequestDtoFromJson(json);
}

@freezed
abstract class ChangePasswordRequestDto with _$ChangePasswordRequestDto {
  const factory ChangePasswordRequestDto({
    required String currentPassword,
    required String newPassword,
  }) = _ChangePasswordRequestDto;

  factory ChangePasswordRequestDto.fromJson(Map<String, dynamic> json) =>
      _$ChangePasswordRequestDtoFromJson(json);
}

@freezed
abstract class TokenDto with _$TokenDto {
  const factory TokenDto({
    required String accessToken,
    required String refreshToken,
    required DateTime expiresAt,
  }) = _TokenDto;

  factory TokenDto.fromJson(Map<String, dynamic> json) =>
      _$TokenDtoFromJson(json);
}

@freezed
abstract class LoginResponseDto with _$LoginResponseDto {
  const factory LoginResponseDto({
    required String userAccountId,
    required String email,
    required bool requiresMfa,
    TokenDto? tokens,
  }) = _LoginResponseDto;

  factory LoginResponseDto.fromJson(Map<String, dynamic> json) =>
      _$LoginResponseDtoFromJson(json);
}

@freezed
abstract class UserAccountDto with _$UserAccountDto {
  const factory UserAccountDto({
    required String id,
    required String email,
    required String status,
    required DateTime createdOn,
    DateTime? verifiedOn,
    DateTime? lastLoginOn,
    @Default(<ExternalIdentityDto>[])
    List<ExternalIdentityDto> externalIdentities,
    @Default(<MfaMethodDto>[]) List<MfaMethodDto> mfaMethods,
    @Default(<DeviceDto>[]) List<DeviceDto> devices,
  }) = _UserAccountDto;

  factory UserAccountDto.fromJson(Map<String, dynamic> json) =>
      _$UserAccountDtoFromJson(json);
}

@freezed
abstract class ExternalIdentityDto with _$ExternalIdentityDto {
  const factory ExternalIdentityDto({
    required String id,
    required String provider,
    required String subject,
    required DateTime linkedOn,
  }) = _ExternalIdentityDto;

  factory ExternalIdentityDto.fromJson(Map<String, dynamic> json) =>
      _$ExternalIdentityDtoFromJson(json);
}

@freezed
abstract class MfaMethodDto with _$MfaMethodDto {
  const factory MfaMethodDto({
    required String id,
    required String type,
    required bool isVerified,
    required bool isActive,
    required DateTime createdOn,
  }) = _MfaMethodDto;

  factory MfaMethodDto.fromJson(Map<String, dynamic> json) =>
      _$MfaMethodDtoFromJson(json);
}

@freezed
abstract class DeviceDto with _$DeviceDto {
  const factory DeviceDto({
    required String id,
    required String name,
    String? platform,
    required DateTime registeredOn,
    required bool isTrusted,
  }) = _DeviceDto;

  factory DeviceDto.fromJson(Map<String, dynamic> json) =>
      _$DeviceDtoFromJson(json);
}

@freezed
abstract class SecurityEventDto with _$SecurityEventDto {
  const factory SecurityEventDto({
    required String id,
    required String eventType,
    String? description,
    required DateTime occurredOn,
  }) = _SecurityEventDto;

  factory SecurityEventDto.fromJson(Map<String, dynamic> json) =>
      _$SecurityEventDtoFromJson(json);
}

/// Enrollment material returned by `POST /mfa/enroll` for a single method.
///
/// `secret` and `provisioningUri` are one-time sensitive enrollment values that
/// must never be logged, persisted, or placed anywhere but transient page
/// state. Only the enrollment [mfaMethodId] may be handed back to the
/// `POST /mfa/enroll/complete` completion call.
@freezed
abstract class MfaEnrollmentDto with _$MfaEnrollmentDto {
  const factory MfaEnrollmentDto({
    required String mfaMethodId,
    required String secret,
    required String provisioningUri,
  }) = _MfaEnrollmentDto;

  factory MfaEnrollmentDto.fromJson(Map<String, dynamic> json) =>
      _$MfaEnrollmentDtoFromJson(json);
}

@freezed
abstract class CompleteMfaEnrollmentRequestDto
    with _$CompleteMfaEnrollmentRequestDto {
  const factory CompleteMfaEnrollmentRequestDto({
    required String mfaMethodId,
    required String code,
  }) = _CompleteMfaEnrollmentRequestDto;

  factory CompleteMfaEnrollmentRequestDto.fromJson(Map<String, dynamic> json) =>
      _$CompleteMfaEnrollmentRequestDtoFromJson(json);
}

/// A read-only session record from `GET /me/sessions`.
///
/// The backend contract carries no IP, network, geographic or current-session
/// fields; the additive nullable [deviceName]/[devicePlatform] resolve from the
/// actor's own owned device rows and are rendered only when present. The UI
/// therefore never fabricates "this device" or device names.
/// [isCurrent] is computed server-side from the signed [sid] claim and never
/// trusts any client-supplied value.
@freezed
abstract class SessionDto with _$SessionDto {
  const factory SessionDto({
    required String id,
    required String deviceId,
    String? deviceName,
    String? devicePlatform,
    required DateTime createdOn,
    required DateTime expiresOn,
    required DateTime lastUsedOn,
    required bool isActive,
    @Default(false) bool isCurrent,
  }) = _SessionDto;

  factory SessionDto.fromJson(Map<String, dynamic> json) =>
      _$SessionDtoFromJson(json);
}
