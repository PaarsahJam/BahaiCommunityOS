import 'package:bloc/bloc.dart';

import '../domain/member_models.dart';
import '../domain/member_repository.dart';
import 'member_session_event.dart';
import 'member_session_state.dart' show MemberSessionState;

/// Bootstraps and owns the authenticated member context for one authenticated
/// session. Created (and closed) by the member shell, so every sign-in starts
/// from a fresh, empty state — previous accounts can never bleed through.
class MemberSessionBloc extends Bloc<MemberSessionEvent, MemberSessionState> {
  MemberSessionBloc(this._repository)
      : super(const MemberSessionState.initial()) {
    on<MemberSessionBootstrapRequested>(_onBootstrapRequested);
  }

  final MemberRepository _repository;

  Future<void> _onBootstrapRequested(
    MemberSessionBootstrapRequested event,
    Emitter<MemberSessionState> emit,
  ) async {
    emit(const MemberSessionState.loading());

    final result = await _repository.loadMemberSession();

    // The shell may have been disposed (sign-out / session expiry) while the
    // bootstrap was in flight. A closed bloc must never emit, which is the
    // deterministic guarantee that an in-flight result from account A can
    // never be published into account B's state.
    if (isClosed) return;

    switch (result) {
      case MemberSessionResolved(
          :final account,
          :final person,
          :final membership
        ):
        emit(
          MemberSessionState.ready(
            MemberContext(
                account: account, person: person, membership: membership),
          ),
        );
      case MemberSessionUnlinked():
        emit(const MemberSessionState.unlinked());
      case MemberSessionUnauthenticated():
        emit(const MemberSessionState.sessionExpired());
      case MemberSessionForbidden(:final error):
        emit(MemberSessionState.forbidden(error));
      case MemberSessionFailed(:final error):
        emit(MemberSessionState.failed(error));
    }
  }
}
