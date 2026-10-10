import 'package:freezed_annotation/freezed_annotation.dart';

part 'activities_dtos.freezed.dart';

@freezed
abstract class ActivityDto with _$ActivityDto {
  const factory ActivityDto({
    required String id,
    required String title,
    String? description,
    String? category,
    String? organizerPersonId,
    String? organizationUnitId,
    String? location,
    bool? isOnline,
    String? onlineUrl,
    required DateTime startsAt,
    DateTime? endsAt,
    required String visibility,
    required String status,
    int? capacity,
    required DateTime createdOn,
  }) = _ActivityDto;

  factory ActivityDto.fromJson(Map<String, dynamic> json) =>
      _$ActivityDtoFromJson(json);
}

@freezed
abstract class ActivityListDto with _$ActivityListDto {
  const factory ActivityListDto({
    required List<ActivityDto> activities,
  }) = _ActivityListDto;

  factory ActivityListDto.fromJson(Map<String, dynamic> json) =>
      _$ActivityListDtoFromJson(json);
}