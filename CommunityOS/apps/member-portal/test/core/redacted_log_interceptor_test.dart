import 'dart:async';

import 'package:dio/dio.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:member_portal/core/network/redacted_log_interceptor.dart';
import 'package:mocktail/mocktail.dart';

class _MockAdapter extends Mock implements HttpClientAdapter {}

void main() {
  const token = 'secret-access-token';
  const refresh = 'secret-refresh-token';
  const authorization = 'Bearer $token';

  Future<List<String>> capturePrint(Future<void> Function() action) async {
    final lines = <String>[];
    await runZoned(
      () => action(),
      zoneSpecification: ZoneSpecification(
        print: (self, parent, zone, line) => lines.add(line),
      ),
    );
    return lines;
  }

  group('RedactedLogInterceptor', () {
    setUpAll(() {
      registerFallbackValue(RequestOptions(path: 'fallback'));
    });

    test('logs method, url and status only when enabled', () async {
      final adapter = _MockAdapter();
      when(() => adapter.fetch(any(), any(), any())).thenAnswer(
        (_) async => ResponseBody.fromString(
          '{"accessToken":"$token","refreshToken":"$refresh"}',
          200,
          headers: {
            Headers.contentTypeHeader: [Headers.jsonContentType]
          },
        ),
      );

      final dio = Dio(BaseOptions(baseUrl: 'http://api.test'));
      dio.httpClientAdapter = adapter;
      dio.interceptors.add(RedactedLogInterceptor(enabled: true));

      final lines = await capturePrint(() async {
        await dio.get('/me',
            options: Options(headers: {
              'Authorization': authorization,
              'X-Custom': 'secret'
            }));
      });

      expect(
          lines.any((l) => l.contains('← 200 GET http://api.test/me')), isTrue);
      for (final line in lines) {
        expect(line.contains(token), isFalse,
            reason: 'access token must never be logged: $line');
        expect(line.contains(refresh), isFalse,
            reason: 'refresh token must never be logged: $line');
        expect(line.toUpperCase().contains('AUTHORIZATION'), isFalse,
            reason: 'headers must never be logged: $line');
        expect(line.contains('X-Custom'), isFalse,
            reason: 'request headers must never be logged: $line');
        expect(line.contains('{"'), isFalse,
            reason: 'request/response bodies must never be logged: $line');
      }
    });

    test('logs nothing when disabled', () async {
      final adapter = _MockAdapter();
      when(() => adapter.fetch(any(), any(), any())).thenAnswer(
        (_) async => ResponseBody.fromString(
          '{"accessToken":"$token"}',
          200,
          headers: {
            Headers.contentTypeHeader: [Headers.jsonContentType]
          },
        ),
      );

      final dio = Dio(BaseOptions(baseUrl: 'http://api.test'));
      dio.httpClientAdapter = adapter;
      dio.interceptors.add(RedactedLogInterceptor());

      final lines = await capturePrint(() async {
        await dio.get('/me');
      });

      expect(lines, isEmpty);
    });
  });
}
