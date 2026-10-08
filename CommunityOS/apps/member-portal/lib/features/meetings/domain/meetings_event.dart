import 'package:freezed_annotation/freezed_annotation.dart';

part 'meetings_event.g.dart';

@freezed
abstract class MeetingsEvent with _$MeetingsEvent implements Equatable {
  const factory MeetingsEvent.initial() = _MeetingsEventInitial;

  const factory MeetingsEvent.loadMeetings() = _MeetingsEventLoadMeetings;

  const factory MeetingsEvent.loadMeetingsFailure() = _MeetingsEventLoadMeetingsFailure;

  const factory MeetingsEvent.loadMeetingsSuccess(List<MeetingDto> meetings) =
      _MeetingsEventLoadMeetingsSuccess;

  const factory MeetingsEvent.loadDetail(String id) = _MeetingsEventLoadDetail;

  const factory MeetingsEvent.loadDetailSuccess(MeetingDto meeting) =
      _MeetingsEventLoadDetailSuccess;

  const factory MeetingsEvent.loadDetailFailure(AppException error) =
      _MeetingsEventLoadDetailFailure;

  const factory MeetingsEvent.refresh() = _MeetingsEventRefresh;

  @override
  List<Object> get props => [];
}