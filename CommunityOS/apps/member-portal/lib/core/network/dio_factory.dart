import 'package:dio/dio.dart';

import '../storage/token_storage.dart';
import 'auth_interceptor.dart';
import 'redacted_log_interceptor.dart';
import 'refresh_coordinator.dart';

/// Builds the two CLIENTS the app talks to the Gateway with:
///
/// * `bare`       — no interceptors. Used for `auth/login` and
///   `auth/refresh` only, so refresh itself can never loop.
/// * `authorized` — installs [AuthInterceptor], which attaches the access
///   token and transparently refreshes + retries one time on 401.
class DioFactory {
  const DioFactory();

  static BaseOptions _baseOptions(String baseUrl) => BaseOptions(
        baseUrl: baseUrl,
        connectTimeout: const Duration(seconds: 15),
        receiveTimeout: const Duration(seconds: 15),
        sendTimeout: const Duration(seconds: 15),
        responseType: ResponseType.json,
        contentType: Headers.jsonContentType,
      );

  Dio buildBare(String baseUrl, {bool logRequests = false}) {
    final dio = Dio(_baseOptions(baseUrl));
    dio.interceptors.add(RedactedLogInterceptor(enabled: logRequests));
    return dio;
  }

  Dio buildAuthorized(
    String baseUrl, {
    required TokenStorage tokenStorage,
    required RefreshCoordinator coordinator,
    bool logRequests = false,
  }) {
    final dio = Dio(_baseOptions(baseUrl));
    dio.interceptors.add(
      AuthInterceptor(tokenStorage, coordinator)..retryClient = dio,
    );
    dio.interceptors.add(RedactedLogInterceptor(enabled: logRequests));
    return dio;
  }

  static Dio forTesting(BaseOptions options) => Dio(options);
}
