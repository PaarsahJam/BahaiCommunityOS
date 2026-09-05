import 'package:freezed_annotation/freezed_annotation.dart';

part 'member_dtos.freezed.dart';
part 'member_dtos.g.dart';

@freezed
abstract class PersonDto with _$PersonDto {
  const factory PersonDto({
    required String id,
    required String preferredName,
    String? formalName,
    String? preferredLanguage,
    required String status,
    required bool hasLinkedIdentityAccount,
    required DateTime createdOn,
  }) = _PersonDto;

  factory PersonDto.fromJson(Map<String, dynamic> json) =>
      _$PersonDtoFromJson(json);
}

@freezed
abstract class PersonDetailDto with _$PersonDetailDto {
  const factory PersonDetailDto({
    required String id,
    required String preferredName,
    String? formalName,
    DateTime? dateOfBirth,
    String? preferredLanguage,
    required String status,
    String? identityAccountId,
    required String profileVisibility,
    required String contactVisibility,
    required String dateOfBirthVisibility,
    @Default(<ContactMethodDto>[]) List<ContactMethodDto> contactMethods,
    required DateTime createdOn,
  }) = _PersonDetailDto;

  factory PersonDetailDto.fromJson(Map<String, dynamic> json) =>
      _$PersonDetailDtoFromJson(json);
}

@freezed
abstract class ContactMethodDto with _$ContactMethodDto {
  const factory ContactMethodDto({
    required String id,
    required String type,
    required String value,
    required bool isPreferred,
    required String visibility,
  }) = _ContactMethodDto;

  factory ContactMethodDto.fromJson(Map<String, dynamic> json) =>
      _$ContactMethodDtoFromJson(json);
}

@freezed
abstract class MembershipDto with _$MembershipDto {
  const factory MembershipDto({
    required String id,
    required String personId,
    required String status,
    required DateTime effectiveFrom,
    DateTime? effectiveUntil,
    DateTime? withdrawnOn,
    @Default(<MembershipPeriodDto>[]) List<MembershipPeriodDto> history,
  }) = _MembershipDto;

  factory MembershipDto.fromJson(Map<String, dynamic> json) =>
      _$MembershipDtoFromJson(json);
}

@freezed
abstract class MembershipPeriodDto with _$MembershipPeriodDto {
  const factory MembershipPeriodDto({
    required String status,
    required DateTime effectiveFrom,
    DateTime? effectiveUntil,
  }) = _MembershipPeriodDto;

  factory MembershipPeriodDto.fromJson(Map<String, dynamic> json) =>
      _$MembershipPeriodDtoFromJson(json);
}
