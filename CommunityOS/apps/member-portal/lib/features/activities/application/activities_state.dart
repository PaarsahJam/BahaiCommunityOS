import 'package:freezed_annotation/freezed_annotation.dart';

import '../../../core/error/app_exception.dart';
import '../data/activities_dtos.dart';

part 'activities_state.freezed.dart';

/// Page-scoped state for the Activities feature.
///
/// [activities] and [activity] are backend responses verbatim. A failed request
/// is carried by [ActivitiesFailed] and is never rendered as an empty list.
@freezed
sealed class ActivitiesState with _$ActivitiesState {
  const factory ActivitiesState.initial() = ActivitiesInitial;

  const factory ActivitiesState.loading() = ActivitiesLoading;

  const factory ActivitiesState.listLoaded({
    required List<ActivityDto> activities,
  }) = ActivitiesListLoaded;

  const factory ActivitiesState.detailLoaded({
    required ActivityDto activity,
  }) = ActivitiesDetailLoaded;

  const factory ActivitiesState.failed(AppException error) = ActivitiesFailed;
}
