import 'package:freezed_annotation/freezed_annotation.dart';

part 'account_event.freezed.dart';

/// Events for one page-scoped [AccountBloc] instance.
///
/// Ownership follows the frozen architecture: the bloc only loads the account
/// overview + read-only security activity and drives the guarded password
/// mutation. Authentication transitions (including the deliberate re-auth
/// after a successful password change) are exclusively the responsibility of
/// `AuthBloc` and its existing session-expiry machinery.
@freezed
sealed class AccountEvent with _$AccountEvent {
  /// Loads the account overview and security activity for the page.
  const factory AccountEvent.requested() = AccountRequested;

  /// Reloads only the read-only security-activity section, leaving the loaded
  /// overview in place.
  const factory AccountEvent.securityEventsRequested() =
      AccountSecurityEventsRequested;

  /// Reloads the whole page after a retryable failure.
  const factory AccountEvent.retryRequested() = AccountRetryRequested;

  /// Submits a password change. The backend is authoritative for policy; the
  /// client never validates password rules (only field presence and
  /// confirmation matching) and never re-submits automatically.
  const factory AccountEvent.passwordChangeRequested({
    required String currentPassword,
    required String newPassword,
  }) = AccountPasswordChangeRequested;
}
