import 'package:bloc/bloc.dart';

import '../../../core/error/app_exception.dart';
import '../../auth/data/auth_dtos.dart';
import '../../member/domain/member_repository.dart';
import 'security_event.dart';
import 'security_state.dart';

/// Drives the read-only session list, per-session revocation, and the guarded
/// TOTP enrollment flow for the Identity & Security section of one account page
/// instance.
///
/// Ownership follows the frozen architecture: this bloc is page-scoped
/// (created and closed by the account page) and is never global. It owns the
/// account's enrollment state and the transient one-time enrollment material,
/// plus the session list and per-session revocation state; the account
/// overview — and therefore the authoritative MFA-enabled state — is owned by
/// `AccountBloc` and the backend. A successful enrollment never flips state
/// directly: the page hands the refresh back to `AccountBloc`, which reloads
/// `/me`. A successful revocation is never fabricated into the list: the bloc
/// re-fetches the authoritative `/me/sessions` after a `204`.
class SecurityBloc extends Bloc<SecurityEvent, SecurityState> {
  SecurityBloc(this._repository) : super(const SecurityState.initial()) {
    on<SecuritySessionsRequested>(_onSessionsRequested);
    on<SecuritySessionRevokeRequested>(_onSessionRevokeRequested);
    on<SecurityRevokeOthersRequested>(_onRevokeOthersRequested);
    on<SecurityMfaEnrollmentRequested>(_onMfaEnrollmentRequested);
    on<SecurityMfaEnrollmentCancelled>(_onMfaEnrollmentCancelled);
    on<SecurityMfaEnrollmentCompleted>(_onMfaEnrollmentCompleted);
    on<SecurityMfaRemovalRequested>(_onMfaRemovalRequested);
    on<SecurityMfaRemovalCancelled>(_onMfaRemovalCancelled);
  }

  final MemberRepository _repository;

  Future<void> _onSessionsRequested(
    SecuritySessionsRequested event,
    Emitter<SecurityState> emit,
  ) async {
    final current = state;
    if (current is SecurityLoaded && current.isSessionsLoading) return;
    await _reloadSessions(emit);
  }

  /// Fetches the authoritative session list. Used by the initial load and
  /// re-fetched after every successful revocation.
  Future<void> _reloadSessions(Emitter<SecurityState> emit) async {
    emit(_with(isSessionsLoading: true));
    final List<SessionDto> sessions;
    try {
      sessions = await _repository.sessions();
      if (isClosed) return;
      emit(_with(
        sessions: sessions,
        isSessionsLoading: false,
        clearSessionsError: true,
      ));
    } on AppException catch (error) {
      // Read-only section failure is non-fatal: keep any prior list and expose
      // a retry. It is never silently conflated with an empty list.
      if (isClosed) return;
      emit(_with(sessionsError: error, isSessionsLoading: false));
    }
  }

  Future<void> _onSessionRevokeRequested(
    SecuritySessionRevokeRequested event,
    Emitter<SecurityState> emit,
  ) async {
    final current = state;
    if (current is! SecurityLoaded) return;
    // One in-flight revoke per session: a duplicate submit is ignored. Other
    // sessions remain independent and revocable.
    if (current.revokingSessionIds.contains(event.sessionId)) return;

    final inFlight = {...current.revokingSessionIds, event.sessionId};
    emit(_with(
      revokingSessionIds: inFlight,
      clearRevokeErrorFor: event.sessionId,
    ));

    bool succeeded = false;
    AppException? error;
    try {
      await _repository.revokeSession(event.sessionId);
      succeeded = true;
    } on AppException catch (e) {
      error = e;
    }
    if (isClosed) return;

    final remaining = {...inFlight}..remove(event.sessionId);
    if (succeeded) {
      // Only a backend 204 is reported as success. The old list entry is never
      // removed or fabricated here; the authoritative list is re-fetched next.
      emit(_with(
        revokingSessionIds: remaining,
        revokedSessionIds: {...current.revokedSessionIds, event.sessionId},
      ));
      await _reloadSessions(emit);
    } else {
      // The session stays visible and retryable; nothing was revoked.
      emit(_with(
        revokingSessionIds: remaining,
        sessionRevokeErrors: {
          ...current.sessionRevokeErrors,
          event.sessionId: error!,
        },
      ));
    }
  }

  Future<void> _onRevokeOthersRequested(
    SecurityRevokeOthersRequested event,
    Emitter<SecurityState> emit,
  ) async {
    final current = state;
    if (current is! SecurityLoaded) return;
    // One in-flight revoke-others operation at a time: a duplicate submit is
    // ignored so the operation can never run twice concurrently.
    if (current.revokingOthers) return;

    emit(_with(
      revokingOthers: true,
      clearRevokeOthersError: true,
    ));

    bool succeeded = false;
    AppException? error;
    try {
      await _repository.revokeOtherSessions();
      succeeded = true;
    } on AppException catch (e) {
      error = e;
    }
    if (isClosed) return;

    if (succeeded) {
      // Only a backend 204 is reported as success. The current session is
      // preserved server-side; the authoritative list is re-fetched next.
      emit(_with(
        revokingOthers: false,
        revokeOthersSucceeded: true,
      ));
      await _reloadSessions(emit);
    } else {
      // Nothing was revoked; keep the operation retryable and never report
      // success.
      emit(_with(
        revokingOthers: false,
        revokeOthersError: error,
      ));
    }
  }

  Future<void> _onMfaEnrollmentRequested(
    SecurityMfaEnrollmentRequested event,
    Emitter<SecurityState> emit,
  ) async {
    final current = state;
    if (current is SecurityLoaded &&
        current.mfaStatus is! MfaEnrollmentIdle &&
        current.mfaStatus is! MfaEnrollmentFailed) {
      // An enrollment is already underway (starting/pending/submitting).
      return;
    }

    emit(_with(mfaStatus: const MfaEnrollmentStatus.starting()));
    try {
      final enrollment = await _repository.beginMfaEnrollment();
      if (isClosed) return;
      emit(_with(
        mfaStatus: MfaEnrollmentStatus.pending(
          methodId: enrollment.mfaMethodId,
          secret: enrollment.secret,
          provisioningUri: enrollment.provisioningUri,
        ),
      ));
    } on AppException catch (error) {
      if (isClosed) return;
      emit(_with(mfaStatus: MfaEnrollmentStatus.failed(error: error)));
    }
  }

  Future<void> _onMfaEnrollmentCancelled(
    SecurityMfaEnrollmentCancelled event,
    Emitter<SecurityState> emit,
  ) async {
    final current = state;
    if (current is! SecurityLoaded) return;
    final status = current.mfaStatus;
    if (status is! MfaEnrollmentPending &&
        status is! MfaEnrollmentSubmitting &&
        status is! MfaEnrollmentFailed) {
      return;
    }
    emit(_with(mfaStatus: const MfaEnrollmentStatus.idle()));
  }

  Future<void> _onMfaEnrollmentCompleted(
    SecurityMfaEnrollmentCompleted event,
    Emitter<SecurityState> emit,
  ) async {
    final current = state;
    if (current is! SecurityLoaded) return;
    final status = current.mfaStatus;
    if (status is! MfaEnrollmentPending) return;

    emit(_with(
      mfaStatus: MfaEnrollmentStatus.submitting(
        methodId: status.methodId,
        secret: status.secret,
        provisioningUri: status.provisioningUri,
      ),
    ));
    try {
      // The method id is always the enrollment returned to this account's own
      // POST /mfa/enroll operation in this page instance — never a route
      // parameter or another user's method.
      await _repository.completeMfaEnrollment(
        mfaMethodId: status.methodId,
        code: event.code,
      );
      if (isClosed) return;
      // Success clears the one-time material. The page then refreshes the
      // authoritative account overview; MFA-enabled rendering follows `/me`.
      emit(_with(mfaStatus: const MfaEnrollmentStatus.succeeded()));
    } on AppException catch (error) {
      // Failure clears the one-time material and stays a localized, retryable
      // error — never "MFA disabled".
      if (isClosed) return;
      emit(_with(mfaStatus: MfaEnrollmentStatus.failed(error: error)));
    }
  }

  Future<void> _onMfaRemovalRequested(
    SecurityMfaRemovalRequested event,
    Emitter<SecurityState> emit,
  ) async {
    final current = state;
    if (current is! SecurityLoaded) return;
    // Only one removal can be in flight at a time.
    if (current.mfaRemovalStatus is MfaRemovalInProgress) return;

    emit(_with(mfaRemovalStatus: const MfaRemovalStatus.inProgress()));
    try {
      await _repository.removeMfaMethod(event.methodId);
      if (isClosed) return;
      emit(_with(mfaRemovalStatus: const MfaRemovalStatus.succeeded()));
    } on AppException catch (error) {
      if (isClosed) return;
      emit(_with(
        mfaRemovalStatus: MfaRemovalStatus.failed(
          methodId: event.methodId,
          error: error,
        ),
      ));
    }
  }

  void _onMfaRemovalCancelled(
    SecurityMfaRemovalCancelled event,
    Emitter<SecurityState> emit,
  ) {
    final current = state;
    if (current is! SecurityLoaded) return;
    // Only a settled removal (failed) can be cancelled from the UI; a no-op
    // reset is harmless for any other state and never fabricates a success.
    emit(_with(mfaRemovalStatus: const MfaRemovalStatus.idle()));
  }

  /// Copies the current loaded sections with the given overrides, treating a
  /// not-yet-loaded state as empty defaults. MFA material, per-session
  /// revocation progress, errors, confirmed-revoked ids, and revoke-others
  /// progress/outcome are preserved across unrelated transitions.
  SecurityState _with({
    List<SessionDto>? sessions,
    bool? isSessionsLoading,
    AppException? sessionsError,
    bool clearSessionsError = false,
    MfaEnrollmentStatus? mfaStatus,
    MfaRemovalStatus? mfaRemovalStatus,
    Set<String>? revokingSessionIds,
    Map<String, AppException>? sessionRevokeErrors,
    String? clearRevokeErrorFor,
    Set<String>? revokedSessionIds,
    bool? revokingOthers,
    AppException? revokeOthersError,
    bool clearRevokeOthersError = false,
    bool? revokeOthersSucceeded,
  }) {
    final prior = state is SecurityLoaded
        ? state as SecurityLoaded
        : const SecurityLoaded();
    final errors = sessionRevokeErrors ?? prior.sessionRevokeErrors;
    final nextErrors = (clearRevokeErrorFor != null &&
            errors.containsKey(clearRevokeErrorFor))
        ? (Map<String, AppException>.of(errors)..remove(clearRevokeErrorFor))
        : errors;
    return SecurityLoaded(
      sessions: sessions ?? prior.sessions,
      isSessionsLoading: isSessionsLoading ?? prior.isSessionsLoading,
      sessionsError:
          clearSessionsError ? null : (sessionsError ?? prior.sessionsError),
      mfaStatus: mfaStatus ?? prior.mfaStatus,
      mfaRemovalStatus: mfaRemovalStatus ?? prior.mfaRemovalStatus,
      revokingSessionIds: revokingSessionIds ?? prior.revokingSessionIds,
      sessionRevokeErrors: nextErrors,
      revokedSessionIds: revokedSessionIds ?? prior.revokedSessionIds,
      revokingOthers: revokingOthers ?? prior.revokingOthers,
      revokeOthersError: clearRevokeOthersError
          ? null
          : (revokeOthersError ?? prior.revokeOthersError),
      revokeOthersSucceeded:
          revokeOthersSucceeded ?? prior.revokeOthersSucceeded,
    );
  }
}
