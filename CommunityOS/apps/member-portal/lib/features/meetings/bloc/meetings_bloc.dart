import 'package:bloc/bloc.dart';

import '../../core/network/error_mapper.dart';
import '../../core/network/refresh_coordinator.dart';
import '../domain/meetings_models.dart';
import '../domain/meetings_event.dart';
import '../domain/meetings_state.dart';
import '../data/meetings_repository.dart';
import 'package:injectable/injectable.dart';

/// Bloc that manages the meetings feature state for the Member Portal.
///
/// Handles loading the meetings list and individual meeting details, including
/// error handling and authentication state management.
///
/// The Bloc is deliberately narrow: it only owns the meetings feature state
/// and delegates all backend I/O to [MeetingsRepository].
@injectable
class MeetingsBloc extends Bloc<MeetingsEvent, MeetingsState> {
  MeetingsBloc(this._repository) : super(const MeetingsState.initial()) {
    on<MeetingsEvent.loadMeetings>(_onLoadMeetings);
    on<MeetingsEvent.loadDetail>(_onLoadDetail);
    on<MeetingsEvent.refresh>(_onRefresh);
  }

  final MeetingsRepository _repository;

  Future<void> _onLoadMeetings(
    MeetingsEvent.loadMeetings event,
    Emitter<MeetingsState> emit,
  ) async {
    emit(const MeetingsState.loading());

    final result = await _repository.listMeetings(
      from: event.from,
      to: event.to,
      organizationUnitId: event.organizationUnitId,
    );

    result.fold(
      (failure) => emit(MeetingsState.failed(failure)),
      (meetings) => emit(MeetingsState.listSuccess(meetings)),
    );
  }

  Future<void> _onLoadDetail(
    MeetingsEvent.loadDetail event,
    Emitter<MeetingsState> emit,
  ) async {
    emit(const MeetingsState.loading());

    final result = await _repository.getMeetingById(event.id);

    result.fold(
      (failure) => emit(MeetingsState.failed(failure)),
      (meeting) => emit(MeetingsState.detailSuccess(meeting)),
    );
  }

  Future<void> _onRefresh(_) async {
    emit(const MeetingsState.initial());
    add(const MeetingsEvent.loadMeetings());
  }
}