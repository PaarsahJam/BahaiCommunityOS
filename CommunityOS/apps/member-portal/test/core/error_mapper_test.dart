import 'package:dio/dio.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:member_portal/core/error/app_exception.dart';
import 'package:member_portal/core/network/error_mapper.dart';

void main() {
  const mapper = ErrorMapper();

  DioException httpError({
    required int statusCode,
    Object? data,
  }) =>
      DioException(
        requestOptions: RequestOptions(path: '/test'),
        type: DioExceptionType.badResponse,
        response: Response<dynamic>(
          requestOptions: RequestOptions(path: '/test'),
          statusCode: statusCode,
          data: data,
        ),
      );

  group('ErrorMapper', () {
    test('maps every timeout type to RequestTimeoutException', () {
      for (final type in [
        DioExceptionType.connectionTimeout,
        DioExceptionType.sendTimeout,
        DioExceptionType.receiveTimeout,
        DioExceptionType.transformTimeout,
      ]) {
        final mapped = mapper.map(DioException(
          requestOptions: RequestOptions(path: '/test'),
          type: type,
        ));
        expect(mapped, isA<RequestTimeoutException>(), reason: 'type=$type');
      }
    });

    test('maps connection and certificate failures to NetworkException', () {
      for (final type in [
        DioExceptionType.connectionError,
        DioExceptionType.badCertificate,
      ]) {
        final mapped = mapper.map(DioException(
          requestOptions: RequestOptions(path: '/test'),
          type: type,
        ));
        expect(mapped, isA<NetworkException>(), reason: 'type=$type');
      }
    });

    test('maps a bare response-less unknown error to NetworkException', () {
      final mapped = mapper.map(DioException(
        requestOptions: RequestOptions(path: '/test'),
        type: DioExceptionType.unknown,
      ));
      expect(mapped, isA<NetworkException>());
    });

    test('maps 400/409/422/429 to ValidationException', () {
      for (final status in [400, 409, 422, 429]) {
        final mapped =
            mapper.map(httpError(statusCode: status, data: {'detail': 'nope'}));
        expect(mapped, isA<ValidationException>(), reason: 'status=$status');
        expect(mapped.statusCode, status);
      }
    });

    test('maps 401 to UnauthorizedException', () {
      final mapped = mapper.map(httpError(statusCode: 401, data: {
        'title': 'Unauthorized',
      }));
      expect(mapped, isA<UnauthorizedException>());
    });

    test('maps 403 to ForbiddenException', () {
      final mapped = mapper.map(httpError(statusCode: 403, data: {}));
      expect(mapped, isA<ForbiddenException>());
    });

    test('maps 404 to NotFoundException', () {
      final mapped = mapper.map(httpError(statusCode: 404, data: {}));
      expect(mapped, isA<NotFoundException>());
    });

    test('maps 5xx to ServerException', () {
      final mapped = mapper.map(httpError(statusCode: 503, data: {}));
      expect(mapped, isA<ServerException>());
    });

    test('extracts problem-details detail and code fields', () {
      final mapped = mapper.map(httpError(statusCode: 422, data: {
        'title': 'Validation failed',
        'detail': 'Email already in use',
        'code': 'EMAIL_IN_USE',
      }));
      expect(mapped.message, 'Email already in use');
      expect(mapped.code, 'EMAIL_IN_USE');
      expect(mapped, isA<ValidationException>());
    });

    test('prefers a string body as the message', () {
      final mapped = mapper.map(httpError(statusCode: 400, data: 'Bad thing'));
      expect(mapped.message, 'Bad thing');
    });
  });
}
