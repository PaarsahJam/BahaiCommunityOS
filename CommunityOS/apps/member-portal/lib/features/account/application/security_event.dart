import 'package:freezed_annotation/freezed_annotation.dart';

part 'security_event.freezed.dart';

/// Events for one page-scoped [SecurityBloc] instance (the Identity & Security
/// section of the account page).
///
/// Ownership follows the frozen architecture: the bloc only drives the
/// read-only session list and the guarded TOTP enrollment flow. Authentication
/// transitions (including the centralized session-expiry path) remain owned by
/// `AuthBloc`; the account overview (which carries the authoritative MFA method
/// list) remains owned by `AccountBloc`.
@freezed
sealed class SecurityEvent with _$SecurityEvent {
  /// Loads (or reloads) the read-only session list for the section.
  const factory SecurityEvent.sessionsRequested() = SecuritySessionsRequested;

  /// Begins TOTP enrollment and reveals the one-time enrollment material.
  const factory SecurityEvent.mfaEnrollmentRequested() =
      SecurityMfaEnrollmentRequested;

  /// Abandons the current enrollment (pending, submitting, or failed) and
  /// clears any one-time enrollment material.
  const factory SecurityEvent.mfaEnrollmentCancelled() =
      SecurityMfaEnrollmentCancelled;

  /// Submits the authenticator's 6-digit [code] to complete the enrollment
  /// opened by the caller's own `POST /mfa/enroll` operation.
  const factory SecurityEvent.mfaEnrollmentCompleted({required String code}) =
      SecurityMfaEnrollmentCompleted;
}
