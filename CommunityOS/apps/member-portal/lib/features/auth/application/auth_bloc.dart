import 'dart:async';

import 'package:bloc/bloc.dart';
import 'package:injectable/injectable.dart';

import '../../../core/network/refresh_coordinator.dart';
import '../domain/auth_models.dart';
import '../domain/auth_repository.dart';
import 'auth_event.dart';
import 'auth_state.dart';

@LazySingleton()
class AuthBloc extends Bloc<AuthEvent, AuthState> {
  AuthBloc(this._repository, this._coordinator)
      : super(const AuthState.initializing()) {
    on<AuthAppStarted>(_onAppStarted);
    on<AuthLoginRequested>(_onLoginRequested);
    on<AuthMfaCodeSubmitted>(_onMfaCodeSubmitted);
    on<AuthLogoutRequested>(_onLogoutRequested);
    on<AuthSessionExpired>(_onSessionExpired);

    _expiredSubscription = _coordinator.onSessionExpired.listen((_) {
      add(const AuthEvent.sessionExpired());
    });
  }

  final AuthRepository _repository;
  final RefreshCoordinator _coordinator;
  StreamSubscription<SessionExpiredEvent>? _expiredSubscription;

  @override
  Future<void> close() {
    _expiredSubscription?.cancel();
    return super.close();
  }

  Future<void> _onAppStarted(
    AuthAppStarted event,
    Emitter<AuthState> emit,
  ) async {
    emit(const AuthState.initializing());

    final tokens = await _repository.restoreSession();
    if (tokens == null) {
      emit(const AuthState.unauthenticated());
      return;
    }

    try {
      final user = await _repository.currentUser();
      emit(AuthState.authenticated(user: user));
    } on UnauthorizedException {
      await _repository.clearLocalAuth();
      emit(const AuthState.unauthenticated());
    } catch (_) {
      // Transient server/network failure: stay signed out for now, but keep
      // the stored credentials for the next launch.
      emit(const AuthState.unauthenticated());
    }
  }

  Future<void> _onLoginRequested(
    AuthLoginRequested event,
    Emitter<AuthState> emit,
  ) async {
    emit(const AuthState.authenticating());
    try {
      final result = await _repository.login(
        email: event.email,
        password: event.password,
      );
      _emitLoginResult(result, emit);
    } on AppException catch (error) {
      emit(AuthState.failure(error: error));
    }
  }

  Future<void> _onMfaCodeSubmitted(
    AuthMfaCodeSubmitted event,
    Emitter<AuthState> emit,
  ) async {
    emit(const AuthState.authenticating());
    try {
      final result = await _repository.resolveMfa(
          email: event.email, mfaCode: event.mfaCode);
      _emitLoginResult(result, emit);
    } on AppException catch (error) {
      emit(AuthState.mfaRequired(email: event.email, error: error));
    }
  }

  void _emitLoginResult(AuthLoginResult result, Emitter<AuthState> emit) {
    switch (result) {
      case AuthRequiresMfa(:final accountId, :final email):
        emit(AuthState.mfaRequired(accountId: accountId, email: email));
      case AuthSignedIn(:final user):
        emit(AuthState.authenticated(user: user));
    }
  }

  Future<void> _onLogoutRequested(
    AuthLogoutRequested event,
    Emitter<AuthState> emit,
  ) async {
    await _repository.logout();
    emit(const AuthState.unauthenticated());
  }

  Future<void> _onSessionExpired(
    AuthSessionExpired event,
    Emitter<AuthState> emit,
  ) async {
    await _repository.clearLocalAuth();
    emit(const AuthState.unauthenticated());
  }
}
