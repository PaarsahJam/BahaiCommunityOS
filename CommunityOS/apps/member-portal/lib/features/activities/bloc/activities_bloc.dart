import 'package:bloc/bloc.dart';

import '../../core/network/error_mapper.dart';
import '../../core/network/refresh_coordinator.dart';
import '../domain/activities_models.dart';
import '../domain/activities_event.dart';
import '../domain/activities_state.dart';
import '../data/activities_repository.dart';
import 'package:injectable/injectable.dart';

/// Bloc that manages the activities feature state for the Member Portal.
///
/// Handles loading the activities list and individual activity details, including
/// error handling and authentication state management.
///
/// The Bloc is deliberately narrow: it only owns the activities feature state
/// and delegates all backend I/O to [ActivitiesRepository].
@injectable
class ActivitiesBloc extends Bloc<ActivitiesEvent, ActivitiesState> {
  ActivitiesBloc(this._repository) : super(const ActivitiesState.initial()) {
    on<ActivitiesEvent.loadActivities>(_onLoadActivities);
    on<ActivitiesEvent.detail>(_onDetail);
    on<ActivitiesEvent.refresh>(_onRefresh);
  }

  final ActivitiesRepository _repository;

  Future<void> _onLoadActivities(
    ActivitiesEvent.loadActivities event,
    Emitter<ActivitiesState> emit,
  ) async {
    emit(const ActivitiesState.loading());

    final result = await _repository.listActivities(
      from: event.from,
      to: event.to,
      organizationUnitId: event.organizationUnitId,
    );

    result.fold(
      (failure) => emit(ActivitiesState.failed(failure)),
      (activities) =>
          emit(ActivitiesState.listSuccess(activities)),
    );
  }

  Future<void> _onDetail(
    ActivitiesEvent.detail event,
    Emitter<ActivitiesState> emit,
  ) async {
    emit(const ActivitiesState.loading());

    final result = await _repository.detailActivity(event.id);

    result.fold(
      (failure) => emit(ActivitiesState.failed(failure)),
      (activity) => emit(ActivitiesState.detailSuccess(activity)),
    );
  }

  Future<void> _onRefresh(_) async {
    emit(const ActivitiesState.initial());
    add(const ActivitiesEvent.loadActivities());
  }
}