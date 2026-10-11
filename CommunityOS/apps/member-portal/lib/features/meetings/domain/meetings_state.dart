import 'package:freezed_annotation/freezed_annotation.dart';

import '../../../core/error/app_exception.dart';
import '../data/meetings_dtos.dart';

part 'meetings_state.freezed.dart';

@freezed
sealed class MeetingsState with _$MeetingsState {
  const MeetingsState._();

  /// Initial state - no meetings have been loaded yet.
  const factory MeetingsState.initial() = MeetingsStateInitial;

  /// Loading state - meetings or detail is being fetched.
  const factory MeetingsState.loading() = MeetingsStateLoading;

  /// Success state for the meetings list.
  ///
  /// Contains the list of meetings available to the member.
  const factory MeetingsState.listSuccess(List<MeetingDto> meetings) =
      MeetingsStateListSuccess;

  /// Success state for a meeting detail.
  ///
  /// Contains the detailed information for a single meeting.
  const factory MeetingsState.detailSuccess(MeetingDto meeting) =
      MeetingsStateDetailSuccess;

  /// Failed state - an error occurred while loading meetings or detail.
  const factory MeetingsState.failed(AppException error) = MeetingsStateFailed;
}
