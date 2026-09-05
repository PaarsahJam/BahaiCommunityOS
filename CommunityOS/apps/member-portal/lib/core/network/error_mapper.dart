import 'package:dio/dio.dart';
import 'package:injectable/injectable.dart';

import '../error/app_exception.dart';

/// Maps [DioException]s and HTTP problem-details responses into typed
/// [AppException]s so feature code can react with pattern matching.
@LazySingleton()
class ErrorMapper {
  const ErrorMapper();

  static const _timeoutMessage = 'The request timed out. Please try again.';
  static const _networkMessage =
      'A network error occurred. Please check your connection and try again.';
  static const _serverMessage =
      'The server could not complete your request. Please try again later.';
  static const _unexpectedMessage =
      'Something unexpected went wrong. Please try again.';

  AppException map(DioException error) {
    switch (error.type) {
      case DioExceptionType.connectionTimeout:
      case DioExceptionType.sendTimeout:
      case DioExceptionType.receiveTimeout:
      case DioExceptionType.transformTimeout:
        return const RequestTimeoutException(_timeoutMessage);
      case DioExceptionType.connectionError:
      case DioExceptionType.badCertificate:
        return const NetworkException(_networkMessage);
      case DioExceptionType.badResponse:
      case DioExceptionType.cancel:
      case DioExceptionType.unknown:
        break;
    }

    final response = error.response;
    final status = response?.statusCode ?? 0;
    if (response == null || status == 0) {
      return const NetworkException(_networkMessage);
    }

    final body = response.data;
    final detail = _problemMessage(body);
    final code = _problemCode(body);

    switch (status) {
      case 400:
      case 409:
      case 422:
      case 429:
        return ValidationException(detail ?? _unexpectedMessage,
            code: code, statusCode: status);
      case 401:
        return UnauthorizedException(detail ?? _unexpectedMessage,
            code: code, statusCode: status);
      case 403:
        return ForbiddenException(detail ?? _unexpectedMessage,
            code: code, statusCode: status);
      case 404:
        return NotFoundException(detail ?? _unexpectedMessage,
            code: code, statusCode: status);
      default:
        if (status >= 500) {
          return ServerException(detail ?? _serverMessage,
              code: code, statusCode: status);
        }
        return UnexpectedException(detail ?? _unexpectedMessage,
            code: code, statusCode: status);
    }
  }

  String? _problemMessage(dynamic body) {
    if (body is Map) {
      for (final key in const ['detail', 'title', 'message']) {
        final value = body[key];
        if (value is String && value.trim().isNotEmpty) return value.trim();
      }
      return null;
    }
    if (body is String && body.trim().isNotEmpty) return body.trim();
    return null;
  }

  String? _problemCode(dynamic body) {
    if (body is Map) {
      final value = body['code'];
      return value is String ? value : null;
    }
    return null;
  }
}
