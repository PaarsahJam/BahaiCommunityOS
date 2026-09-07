import 'package:dio/dio.dart';
import 'package:injectable/injectable.dart';

import '../storage/token_storage.dart';
import 'refresh_coordinator.dart';

/// Attaches the current access token to authenticated requests and performs
/// exactly one automatic refresh+retry when a request is rejected with 401.
///
/// Refresh calls and retried requests are safe from recursion: the refresh
/// request runs on a separate bare client (no interceptors) and each retried
/// request is marked before re-sending.
///
/// A request can opt out of the transparent refresh+retry by carrying
/// `extra[noAutoRetryKey] == true`. This is required for non-idempotent
/// mutations (e.g. password change): such endpoints must never be re-submitted
/// automatically after a 401, and their 401 responses are surfaced verbatim so
/// the caller can decide semantics that an HTTP status code alone cannot.
@LazySingleton()
class AuthInterceptor extends Interceptor {
  AuthInterceptor(this._tokenStorage, this._refreshCoordinator);

  final TokenStorage _tokenStorage;
  final RefreshCoordinator _refreshCoordinator;

  /// The client used to re-send a request after a successful refresh. Set by
  /// the network module to the client this interceptor is installed on.
  Dio? retryClient;

  static const _retriedKey = 'auth_already_retried';

  /// Request-options extra flag that opts a request out of the automatic
  /// refresh+retry path. Set by non-idempotent mutations only.
  static const noAutoRetryKey = 'no_auto_retry';

  // Defense in depth: these endpoints are never used on the authenticated
  // client, but must never be intercepted if they ever were.
  static const _anonymousPathMarkers = ['/auth/login', '/auth/refresh'];

  @override
  Future<void> onRequest(
    RequestOptions options,
    RequestInterceptorHandler handler,
  ) async {
    if (_isAnonymous(options.path)) {
      return handler.next(options);
    }
    final tokens = await _tokenStorage.read();
    if (tokens != null && tokens.accessToken.isNotEmpty) {
      options.headers['Authorization'] = 'Bearer ${tokens.accessToken}';
    }
    handler.next(options);
  }

  @override
  Future<void> onError(
    DioException err,
    ErrorInterceptorHandler handler,
  ) async {
    if (err.response?.statusCode != 401 ||
        err.requestOptions.extra[_retriedKey] == true ||
        err.requestOptions.extra[noAutoRetryKey] == true ||
        _isAnonymous(err.requestOptions.path)) {
      return handler.next(err);
    }

    final tokens = await _refreshCoordinator.refreshTokens();
    if (tokens == null) {
      // Refresh failed; coordinator has already cleared credentials and
      // broadcast the session-expired event.
      return handler.next(err);
    }

    final options = err.requestOptions;
    options.extra[_retriedKey] = true;
    options.headers['Authorization'] = 'Bearer ${tokens.accessToken}';

    final client = retryClient;
    if (client == null) {
      return handler.next(err);
    }

    try {
      final response = await client.fetch<dynamic>(options);
      return handler.resolve(response);
    } on DioException catch (retryError) {
      return handler.next(retryError);
    }
  }

  bool _isAnonymous(String path) => _anonymousPathMarkers.any(path.contains);
}
