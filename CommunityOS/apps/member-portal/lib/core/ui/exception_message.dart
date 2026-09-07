import 'package:flutter/widgets.dart';
import 'package:member_portal/l10n/generated/app_localizations.dart';

import '../error/app_exception.dart';

/// Renders a user-facing message for an [AppException], preferring the
/// localization identified by [AppException.messageKey] and falling back to
/// server-provided detail.
String exceptionMessage(BuildContext context, AppException? error) {
  final l10n = AppLocalizations.of(context);
  if (error == null) return l10n?.commonError ?? 'Something went wrong.';

  switch (error.messageKey) {
    case 'login_invalidCredentials':
      return l10n?.loginInvalidCredentials ?? error.message;
    case 'mfa_invalidCode':
      return l10n?.mfaInvalidCode ?? error.message;
    case 'profile_notFound':
      return l10n?.profileNotFound ?? error.message;
    case 'profile_forbidden':
      return l10n?.profileForbidden ?? error.message;
    case 'membership_notFound':
      return l10n?.membershipNotFound ?? error.message;
    case 'membership_forbidden':
      return l10n?.membershipForbidden ?? error.message;
    case 'account_notFound':
      return l10n?.accountNotFound ?? error.message;
    case 'account_forbidden':
      return l10n?.accountForbidden ?? error.message;
    case 'accountCurrentPasswordIncorrect':
      return l10n?.accountCurrentPasswordIncorrect ?? error.message;
    case 'accountPasswordRetryable':
      return l10n?.accountPasswordRetryable ?? error.message;
    case 'accountPasswordValidationFailed':
      return l10n?.accountPasswordValidationFailed ?? error.message;
  }

  if (error.message.trim().isEmpty) {
    return l10n?.commonError ?? 'Something went wrong.';
  }
  return error.message;
}
