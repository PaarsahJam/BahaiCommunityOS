/// Base application-level exception produced by mapping infrastructure
/// failures (Dio errors, HTTP statuses) into domain terms.
///
/// [message] carries server-provided detail when available; [messageKey] is an
/// optional localization key the UI may use to render a friendlier message.
class AppException implements Exception {
  const AppException(
    this.message, {
    this.messageKey,
    this.code,
    this.statusCode,
  });

  final String message;
  final String? messageKey;
  final String? code;
  final int? statusCode;

  @override
  String toString() =>
      'AppException(statusCode: $statusCode, code: $code): $message';
}

/// No route to the API server (DNS, refused connection, etc).
class NetworkException extends AppException {
  const NetworkException(super.message, {super.code, super.statusCode});
}

/// The server did not respond in time.
class RequestTimeoutException extends AppException {
  const RequestTimeoutException(super.message, {super.code, super.statusCode});
}

/// The caller's session/credentials are missing, invalid or expired.
class UnauthorizedException extends AppException {
  const UnauthorizedException(super.message,
      {super.messageKey, super.code, super.statusCode});
}

/// The caller is authenticated but lacks the required permission.
class ForbiddenException extends AppException {
  const ForbiddenException(super.message,
      {super.messageKey, super.code, super.statusCode});
}

/// The requested resource does not exist.
class NotFoundException extends AppException {
  const NotFoundException(super.message,
      {super.messageKey, super.code, super.statusCode});
}

/// The request was rejected because of invalid input/state (400/409/422).
class ValidationException extends AppException {
  const ValidationException(super.message,
      {super.messageKey, super.code, super.statusCode});
}

/// A 5xx response from the API server (or gateway).
class ServerException extends AppException {
  const ServerException(super.message, {super.code, super.statusCode});
}

/// Anything that did not fit one of the categories above.
class UnexpectedException extends AppException {
  const UnexpectedException(super.message,
      {super.messageKey, super.code, super.statusCode});
}
