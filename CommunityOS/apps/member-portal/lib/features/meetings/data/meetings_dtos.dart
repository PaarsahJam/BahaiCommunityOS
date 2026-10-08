import 'package:freezed_annotation/freezed_annotation.dart';

part 'meetings_dtos.freezed.dart';
part 'meetings_dtos.g.dart';

@freezed
abstract class MeetingDto with _$MeetingDto {
  const factory MeetingDto({
    required String id,
    required String title,
    String? description,
    DateTime? startsAt,
    DateTime? endsAt,
    String? timeZone,
    String? location,
    String? organizerPersonId,
    String? organizationUnitId,
    String status = 'scheduled',
    String visibility = 'members',
    int? capacity,
    bool? registrationOpen,
  }) = _MeetingDto;

  factory MeetingDto.fromJson(Map<String, dynamic> json) =>
      _$MeetingDtoFromJson(json);
}

@freezed
abstract class MeetingListDto with _$MeetingListDto {
  const factory MeetingListDto({
    required List<MeetingDto> meetings,
  }) = _MeetingListDto;

  factory MeetingListDto.fromJson(Map<String, dynamic> json) =>
      _$MeetingListDtoFromJson(json);
}