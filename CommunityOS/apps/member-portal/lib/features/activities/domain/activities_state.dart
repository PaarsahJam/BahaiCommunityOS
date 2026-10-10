import 'package:freezed_annotation/freezed_annotation.dart';

part 'activities_state.g.dart';

@freezed
abstract class ActivitiesState with _$ActivitiesState implements Equatable {
  const ActivitiesState._();

  /// Initial state - no activities have been loaded yet.
  const factory ActivitiesState.initial() = _ActivitiesStateInitial;

  /// Loading state - activities or detail is being fetched.
  const factory ActivitiesState.loading() = _ActivitiesStateLoading;

  /// Success state for the activities list.
  ///
  /// Contains the list of activities available to the member.
  const factory ActivitiesState.listSuccess(
    List<ActivityDto> activities,
  ) = _ActivitiesStateListSuccess;

  /// Success state for an activity detail.
  ///
  /// Contains the detailed information for a single activity.
  const factory ActivitiesState.detailSuccess(ActivityDto activity) =
      _ActivitiesStateDetailSuccess;

  /// Failed state - an error occurred while loading activities or detail.
  const factory ActivitiesState.failed(AppException error) = _ActivitiesStateFailed;

  @override
  List<Object> get props =>
      switch (this) {
        ActivitiesState.initial() => [],
        ActivitiesState.loading() => [],
        ActivitiesState.listSuccess(activities) => [activities],
        ActivitiesState.detailSuccess(activity) => [activity],
        ActivitiesState.failed(error) => [error],
      };
}