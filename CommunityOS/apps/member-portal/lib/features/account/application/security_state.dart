import 'package:freezed_annotation/freezed_annotation.dart';

import '../../../core/error/app_exception.dart';
import '../../auth/data/auth_dtos.dart';

part 'security_state.freezed.dart';

/// Lifecycle of the ONE-time TOTP enrollment flow owned by a page-scoped
/// [SecurityBloc] instance.
///
/// SECURITY: the enrollment material ([MfaEnrollmentPending.secret] and
/// `.provisioningUri`) exists only inside these transient, page-scoped states.
/// It is cleared on success, failure, and cancellation; it is never logged,
/// persisted, exposed outside the owning page, or retained once the bloc is
/// closed and collected.
@freezed
sealed class MfaEnrollmentStatus with _$MfaEnrollmentStatus {
  const factory MfaEnrollmentStatus.idle() = MfaEnrollmentIdle;

  const factory MfaEnrollmentStatus.starting() = MfaEnrollmentStarting;

  const factory MfaEnrollmentStatus.pending({
    required String methodId,
    required String secret,
    required String provisioningUri,
  }) = MfaEnrollmentPending;

  const factory MfaEnrollmentStatus.submitting({
    required String methodId,
    required String secret,
    required String provisioningUri,
  }) = MfaEnrollmentSubmitting;

  const factory MfaEnrollmentStatus.succeeded() = MfaEnrollmentSucceeded;

  const factory MfaEnrollmentStatus.failed({AppException? error}) =
      MfaEnrollmentFailed;
}

/// Page-scoped state for the Identity & Security section rendered inside the
/// account page: the read-only session list plus the MFA enrollment flow.
///
/// Read-only session failures are non-fatal (the section renders its own error
/// + retry) and are never conflated with an empty session list. MFA enrollment
/// success never marks the account as MFA-enabled on its own — the page asks
/// the existing [AccountBloc] to refresh `/me`, and the authoritative method
/// list drives the enabled state.
@freezed
sealed class SecurityState with _$SecurityState {
  const factory SecurityState.initial() = SecurityInitial;

  const factory SecurityState.loaded({
    @Default(<SessionDto>[]) List<SessionDto> sessions,
    @Default(false) bool isSessionsLoading,
    AppException? sessionsError,
    @Default(MfaEnrollmentStatus.idle()) MfaEnrollmentStatus mfaStatus,
  }) = SecurityLoaded;
}
