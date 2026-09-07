import 'package:bloc/bloc.dart';

import '../../../core/error/app_exception.dart';
import '../../auth/data/auth_dtos.dart';
import '../../member/domain/member_repository.dart';
import 'security_event.dart';
import 'security_state.dart';

/// Drives the read-only session list and the guarded TOTP enrollment flow for
/// the Identity & Security section of one account page instance.
///
/// Ownership follows the frozen architecture: this bloc is page-scoped
/// (created and closed by the account page) and is never global. It owns the
/// account's enrollment state and the transient one-time enrollment material,
/// and the sessions loading state; the account overview — and therefore the
/// authoritative MFA-enabled state — is owned by `AccountBloc` and the backend.
/// A successful enrollment never flips state directly: the page hands the
/// refresh back to `AccountBloc`, which reloads `/me`.
class SecurityBloc extends Bloc<SecurityEvent, SecurityState> {
  SecurityBloc(this._repository) : super(const SecurityState.initial()) {
    on<SecuritySessionsRequested>(_onSessionsRequested);
    on<SecurityMfaEnrollmentRequested>(_onMfaEnrollmentRequested);
    on<SecurityMfaEnrollmentCancelled>(_onMfaEnrollmentCancelled);
    on<SecurityMfaEnrollmentCompleted>(_onMfaEnrollmentCompleted);
  }

  final MemberRepository _repository;

  Future<void> _onSessionsRequested(
    SecuritySessionsRequested event,
    Emitter<SecurityState> emit,
  ) async {
    final current = state;
    if (current is SecurityLoaded && current.isSessionsLoading) return;
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

  /// Copies the current loaded sections with the given overrides, treating a
  /// not-yet-loaded state as empty defaults. MFA material is preserved across
  /// non-MFA transitions and vice versa.
  SecurityState _with({
    List<SessionDto>? sessions,
    bool? isSessionsLoading,
    AppException? sessionsError,
    bool clearSessionsError = false,
    MfaEnrollmentStatus? mfaStatus,
  }) {
    final prior = state is SecurityLoaded
        ? state as SecurityLoaded
        : const SecurityLoaded();
    return SecurityLoaded(
      sessions: sessions ?? prior.sessions,
      isSessionsLoading: isSessionsLoading ?? prior.isSessionsLoading,
      sessionsError:
          clearSessionsError ? null : (sessionsError ?? prior.sessionsError),
      mfaStatus: mfaStatus ?? prior.mfaStatus,
    );
  }
}
