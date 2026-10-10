import 'package:freezed_annotation/freezed_annotation.dart';

part 'activities_event.g.dart';

@freezed
abstract class ActivitiesEvent with _$ActivitiesEvent implements Equatable {
  const factory ActivitiesEvent.initial() = _ActivitiesEventInitial;

  const factory ActivitiesEvent.loadActivities() = _ActivitiesEventLoadActivities;

  const factory ActivitiesEvent.loadActivitiesFailure() =
      _ActivitiesEventLoadActivitiesFailure;

  const factory ActivitiesEvent.loadActivitiesSuccess(
    List<ActivityDto> activities,
  ) = _ActivitiesEventLoadActivitiesSuccess;

  const factory ActivitiesEvent.detail(String id) = _ActivitiesEventDetail;

  const factory ActivitiesEvent.detailSuccess(ActivityDto activity) =
      _ActivitiesEventDetailSuccess;

  const factory ActivitiesEvent.detailFailure(AppException error) =
      _ActivitiesEventDetailFailure;

  const factory ActivitiesEvent.refresh() = _ActivitiesEventRefresh;

  @override
  List<Object> get props => [];
}