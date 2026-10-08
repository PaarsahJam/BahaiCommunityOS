import 'package:freezed_annotation/freezed_annotation.dart';

part 'meetings_state.g.dart';

@freezed
abstract class MeetingsState with _$MeetingsState implements Equatable {
  const MeetingsState._();

  /// Initial state - no meetings have been loaded yet.
  const factory MeetingsState.initial() = _MeetingsStateInitial;

  /// Loading state - meetings or detail is being fetched.
  const factory MeetingsState.loading() = _MeetingsStateLoading;

  /// Success state for the meetings list.
  ///
  /// Contains the list of meetings available to the member.
  const factory MeetingsState.listSuccess(List<MeetingDto> meetings) =
      _MeetingsStateListSuccess;

  /// Success state for a meeting detail.
  ///
  /// Contains the detailed information for a single meeting.
  const factory MeetingsState.detailSuccess(MeetingDto meeting) =
      _MeetingsStateDetailSuccess;

  /// Failed state - an error occurred while loading meetings or detail.
  const factory MeetingsState.failed(AppException error) = _MeetingsStateFailed;

  @override
  List<Object> get props =>
      switch (this) {
        MeetingsState.initial() => [],
        MeetingsState.loading() => [],
        MeetingsState.listSuccess(meetings) => [meetings],
        MeetingsState.detailSuccess(meeting) => [meeting],
        MeetingsState.failed(error) => [error],
      };
}