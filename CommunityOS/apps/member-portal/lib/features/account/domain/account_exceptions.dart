import '../../../core/error/app_exception.dart';

/// A password mutation rejected with a FluentValidation problem-details
/// payload (HTTP 400) carrying per-field validation errors from the backend.
///
/// The backend remains the single authority on password policy; this type only
/// carries the server's per-field messages so the form can surface them next
/// to the offending input. It is never synthesized client-side.
class PasswordValidationException extends AppException {
  const PasswordValidationException(
    super.message, {
    required this.fieldErrors,
    super.messageKey,
    super.code,
    super.statusCode,
  });

  /// Backend property name (lowercased, e.g. `currentpassword`) →
  /// server-provided error message for that field.
  final Map<String, String> fieldErrors;
}
