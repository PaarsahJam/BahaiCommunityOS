import 'package:freezed_annotation/freezed_annotation.dart';

import '../domain/auth_models.dart';

part 'auth_state.freezed.dart';

@freezed
sealed class AuthState with _$AuthState {
  const AuthState._();

  const factory AuthState.initializing() = AuthInitializing;

  const factory AuthState.unauthenticated() = AuthUnauthenticated;

  const factory AuthState.authenticating() = AuthAuthenticating;

  const factory AuthState.mfaRequired({
    String? accountId,
    required String email,
    AppException? error,
  }) = AuthMfaRequired;

  const factory AuthState.authenticated({required AuthUser user}) =
      AuthAuthenticated;

  const factory AuthState.failure({required AppException error}) = AuthFailure;
}
