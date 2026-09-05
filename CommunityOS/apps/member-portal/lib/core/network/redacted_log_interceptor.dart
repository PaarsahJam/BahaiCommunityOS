import 'package:dio/dio.dart';

import '../config/app_config.dart';

/// Dev-only request/response logger. Prints method + URL + status only and
/// never bodies or headers, so credentials/tokens can never be logged.
class RedactedLogInterceptor extends Interceptor {
  RedactedLogInterceptor({this.enabled = false});

  final bool enabled;

  void _log(String message) {
    if (enabled) {
      // ignore: avoid_print
      print(message);
    }
  }

  @override
  void onRequest(RequestOptions options, RequestInterceptorHandler handler) {
    _log('→ ${options.method} ${options.uri}');
    handler.next(options);
  }

  @override
  void onResponse(Response response, ResponseInterceptorHandler handler) {
    _log('← ${response.statusCode} '
        '${response.requestOptions.method} ${response.requestOptions.uri}');
    handler.next(response);
  }

  @override
  void onError(DioException err, ErrorInterceptorHandler handler) {
    _log('✗ ${err.response?.statusCode ?? err.type} '
        '${err.requestOptions.method} ${err.requestOptions.uri}');
    handler.next(err);
  }
}

/// Convenience factory used by the DI network module.
RedactedLogInterceptor buildLogInterceptor(AppConfig config) =>
    RedactedLogInterceptor(enabled: config.logRequests);
