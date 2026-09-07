import 'package:dio/dio.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:member_portal/core/network/auth_interceptor.dart';
import 'package:member_portal/core/network/dio_factory.dart';
import 'package:member_portal/core/network/redacted_log_interceptor.dart';
import 'package:member_portal/core/network/refresh_coordinator.dart';
import 'package:member_portal/core/storage/token_storage.dart';
import 'package:mocktail/mocktail.dart';

class _MockTokenStorage extends Mock implements TokenStorage {}

class _MockCoordinator extends Mock implements RefreshCoordinator {}

void main() {
  const factory = DioFactory();
  const baseUrl = 'http://localhost:5000/api/v1';

  test('the bare client carries no auth interceptor so refresh can never loop',
      () {
    final dio = factory.buildBare(baseUrl);

    expect(dio.options.baseUrl, baseUrl);
    expect(dio.interceptors.whereType<AuthInterceptor>(), isEmpty);
    final log = dio.interceptors.whereType<RedactedLogInterceptor>().single;
    expect(log.enabled, isFalse);
  });

  test('the authorized client binds the auth interceptor to its own dio', () {
    final storage = _MockTokenStorage();
    final coordinator = _MockCoordinator();
    final dio = factory.buildAuthorized(
      baseUrl,
      tokenStorage: storage,
      coordinator: coordinator,
    );

    final auth = dio.interceptors.whereType<AuthInterceptor>().single;
    expect(auth.retryClient, same(dio));
  });

  test('the redacting logger is enabled only when logRequests is requested',
      () {
    final storage = _MockTokenStorage();
    final coordinator = _MockCoordinator();
    final logged = factory.buildAuthorized(
      baseUrl,
      tokenStorage: storage,
      coordinator: coordinator,
      logRequests: true,
    );

    expect(
      logged.interceptors.whereType<RedactedLogInterceptor>().single.enabled,
      isTrue,
    );
    expect(
      factory
          .buildBare(baseUrl)
          .interceptors
          .whereType<RedactedLogInterceptor>()
          .single
          .enabled,
      isFalse,
    );
  });

  test('base options carry 15s timeouts and a JSON content type', () {
    final dio = factory.buildBare(baseUrl);

    expect(dio.options.connectTimeout, const Duration(seconds: 15));
    expect(dio.options.receiveTimeout, const Duration(seconds: 15));
    expect(dio.options.sendTimeout, const Duration(seconds: 15));
    expect(dio.options.responseType, ResponseType.json);
    expect(dio.options.contentType, Headers.jsonContentType);
  });
}
