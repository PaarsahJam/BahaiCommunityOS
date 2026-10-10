import 'package:bloc/bloc.dart';

import '../../../core/error/app_exception.dart';
import '../domain/activities_repository.dart';
import 'activities_event.dart';
import 'activities_state.dart';

/// Loads the authenticated member's activities and individual activity details
/// for one Activities page instance.
///
/// Ownership follows the frozen architecture: this bloc is page-scoped (created
/// and closed by the page), never global, and never emits authentication
/// transitions — the auth bloc and the centralized session-expiry machinery
/// exclusively own those. The backend remains the single authority for scope,
/// content and authorization; a failed request is carried by
/// [ActivitiesFailed] and is never rendered as an empty list.
class ActivitiesBloc extends Bloc<ActivitiesEvent, ActivitiesState> {
  ActivitiesBloc(this._repository) : super(const ActivitiesState.initial()) {
    on<ActivitiesRequested>(_onRequested);
    on<ActivitiesDetailRequested>(_onDetail);
  }

  final ActivitiesRepository _repository;

  Future<void> _onRequested(
    ActivitiesRequested event,
    Emitter<ActivitiesState> emit,
  ) =>
      _load(
        emit,
        from: event.from,
        to: event.to,
        organizationUnitId: event.organizationUnitId,
      );

  Future<void> _load(
    Emitter<ActivitiesState> emit, {
    DateTime? from,
    DateTime? to,
    String? organizationUnitId,
  }) async {
    emit(const ActivitiesState.loading());
    try {
      final activities = await _repository.listActivities(
        from: from,
        to: to,
        organizationUnitId: organizationUnitId,
      );
      // The page may have been popped (disposed) while loading; a closed bloc
      // must never emit.
      if (isClosed) return;
      emit(ActivitiesState.listLoaded(activities: activities));
    } on AppException catch (error) {
      if (isClosed) return;
      emit(ActivitiesState.failed(error));
    }
  }

  Future<void> _onDetail(
    ActivitiesDetailRequested event,
    Emitter<ActivitiesState> emit,
  ) async {
    emit(const ActivitiesState.loading());
    try {
      final activity = await _repository.detailActivity(event.id);
      if (isClosed) return;
      emit(ActivitiesState.detailLoaded(activity: activity));
    } on AppException catch (error) {
      if (isClosed) return;
      emit(ActivitiesState.failed(error));
    }
  }
}
