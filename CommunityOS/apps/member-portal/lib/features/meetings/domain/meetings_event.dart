import 'package:freezed_annotation/freezed_annotation.dart';

import '../../../core/error/app_exception.dart';
import '../data/meetings_dtos.dart';

part 'meetings_event.freezed.dart';

@freezed
sealed class MeetingsEvent with _$MeetingsEvent {
  const factory MeetingsEvent.initial() = MeetingsEventInitial;

  const factory MeetingsEvent.loadMeetings({
    DateTime? from,
    DateTime? to,
    String? organizationUnitId,
  }) = MeetingsLoadMeetings;

  const factory MeetingsEvent.loadMeetingsFailure() =
      MeetingsLoadMeetingsFailure;

  const factory MeetingsEvent.loadMeetingsSuccess(List<MeetingDto> meetings) =
      MeetingsLoadMeetingsSuccess;

  const factory MeetingsEvent.loadDetail(String id) = MeetingsLoadDetail;

  const factory MeetingsEvent.loadDetailSuccess(MeetingDto meeting) =
      MeetingsLoadDetailSuccess;

  const factory MeetingsEvent.loadDetailFailure(AppException error) =
      MeetingsLoadDetailFailure;

  const factory MeetingsEvent.refresh() = MeetingsRefresh;
}
