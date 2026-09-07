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
