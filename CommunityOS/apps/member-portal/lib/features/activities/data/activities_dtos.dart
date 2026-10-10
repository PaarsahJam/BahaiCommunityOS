import 'package:freezed_annotation/freezed_annotation.dart';

part 'activities_dtos.freezed.dart';
part 'activities_dtos.g.dart';

/// Member-safe activity record from `GET /activities` and
/// `GET /activities/{id}`.
///
/// Mirrors the committed community activity read contract exactly. The backend
/// remains authoritative for all data and authorization; the client only maps
/// the response into typed values and never re-authorizes locally.
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
    required bool isOnline,
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
