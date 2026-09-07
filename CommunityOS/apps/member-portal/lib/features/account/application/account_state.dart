import 'package:freezed_annotation/freezed_annotation.dart';

import '../../../core/error/app_exception.dart';
import '../../auth/data/auth_dtos.dart';

part 'account_state.freezed.dart';

/// Lifecycle of the guarded password-change mutation rendered inside the
/// loaded account page.
///
/// The backend is the single authority on password policy; these states only
/// shape how the form renders progress and outcomes and never re-declare a
/// rule. A success hands control back to the centralized re-authentication
/// path because the backend revokes the whole token family.
@freezed
sealed class PasswordStatus with _$PasswordStatus {
  const factory PasswordStatus.idle() = PasswordIdle;
  const factory PasswordStatus.submitting() = PasswordSubmitting;
  const factory PasswordStatus.succeeded() = PasswordSucceeded;
  const factory PasswordStatus.failed({AppException? error}) = PasswordFailed;
}

/// Page-scoped state for the account page: the authoritative `/me` overview
/// plus the read-only recent security activity. Security-activity failures are
/// non-fatal (the section renders its own error + retry), while password
/// outcomes live inside [AccountLoaded.passwordStatus].
@freezed
sealed class AccountState with _$AccountState {
  const factory AccountState.initial() = AccountInitial;

  const factory AccountState.loading() = AccountLoading;

  const factory AccountState.loaded({
    required UserAccountDto account,
    @Default(<SecurityEventDto>[]) List<SecurityEventDto> securityEvents,
    @Default(false) bool isSecurityEventsLoading,
    AppException? securityEventsError,
    @Default(PasswordStatus.idle()) PasswordStatus passwordStatus,
  }) = AccountLoaded;

  const factory AccountState.failed(AppException error) = AccountFailed;
}
