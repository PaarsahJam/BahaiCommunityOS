import 'package:dio/dio.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:member_portal/core/network/auth_interceptor.dart';
import 'package:member_portal/features/auth/data/auth_api.dart';
import 'package:mocktail/mocktail.dart';

class _MockAdapter extends Mock implements HttpClientAdapter {}

void main() {
  group('AccountApi (session wire contract)', () {
    late _MockAdapter adapter;
    late AccountApi api;

    setUpAll(() {
      registerFallbackValue(RequestOptions(path: 'fallback'));
    });

    setUp(() {
      adapter = _MockAdapter();
      final dio = Dio(BaseOptions(baseUrl: 'http://api.test'));
      dio.httpClientAdapter = adapter;
      api = AccountApi(dio);
    });

    ResponseBody jsonBody(String body, [int status = 200]) =>
        ResponseBody.fromString(
          body,
          status,
          headers: {
            Headers.contentTypeHeader: [Headers.jsonContentType]
          },
        );

    test('sessions GETs the caller-scoped read-only endpoint', () async {
      when(() => adapter.fetch(any(), any(), any())).thenAnswer(
        (_) async => jsonBody('''
          [{
            "id": "s1",
            "deviceId": "d1",
            "deviceName": "Back office terminal",
            "devicePlatform": "Windows",
            "createdOn": "2026-09-01T09:00:00Z",
            "expiresOn": "2026-09-08T09:00:00Z",
            "lastUsedOn": "2026-09-07T12:00:00Z",
            "isActive": true
          }]
        '''),
      );

      final items = await api.sessions();

      final options = verify(() => adapter.fetch(captureAny(), any(), any()))
          .captured
          .single as RequestOptions;
      expect(options.method, 'GET');
      expect(options.path, 'me/sessions');
      expect(options.queryParameters, isEmpty);
      // The caller is resolved from the token; no identifier travels.
      expect(options.path.contains('member'), isFalse);
      expect(options.path.contains('account'), isFalse);
      expect(items.single.id, 's1');
      expect(items.single.isActive, isTrue);
      expect(items.single.isCurrent, isFalse);
      expect(items.single.deviceId, 'd1');
      expect(items.single.deviceName, 'Back office terminal');
      expect(items.single.devicePlatform, 'Windows');
    });

    test('SessionDto tolerates absent optional device metadata', () async {
      when(() => adapter.fetch(any(), any(), any())).thenAnswer(
        (_) async => jsonBody('''
          [{
            "id": "s1",
            "deviceId": "d1",
            "createdOn": "2026-09-01T09:00:00Z",
            "expiresOn": "2026-09-08T09:00:00Z",
            "lastUsedOn": "2026-09-07T12:00:00Z",
            "isActive": false
          }]
        '''),
      );

      final items = await api.sessions();

      // Older responses without the additive fields remain deserializable.
      expect(items.single.id, 's1');
      expect(items.single.deviceName, isNull);
      expect(items.single.devicePlatform, isNull);
      expect(items.single.isActive, isFalse);
      expect(items.single.isCurrent, isFalse);
    });

    test('SessionDto parses an explicit current-session flag', () async {
      when(() => adapter.fetch(any(), any(), any())).thenAnswer(
        (_) async => jsonBody('''
          [{
            "id": "s1",
            "deviceId": "d1",
            "createdOn": "2026-09-01T09:00:00Z",
            "expiresOn": "2026-09-08T09:00:00Z",
            "lastUsedOn": "2026-09-07T12:00:00Z",
            "isActive": true,
            "isCurrent": true
          }]
        '''),
      );

      final items = await api.sessions();

      expect(items.single.isCurrent, isTrue);
      expect(items.single.isActive, isTrue);
    });

    test('SessionDto parses an explicit non-current session', () async {
      when(() => adapter.fetch(any(), any(), any())).thenAnswer(
        (_) async => jsonBody('''
          [{
            "id": "s2",
            "deviceId": "d2",
            "createdOn": "2026-09-01T09:00:00Z",
            "expiresOn": "2026-09-08T09:00:00Z",
            "lastUsedOn": "2026-09-07T12:00:00Z",
            "isActive": true,
            "isCurrent": false
          }]
        '''),
      );

      final items = await api.sessions();

      expect(items.single.isCurrent, isFalse);
    });

    test(
        'revokeSession POSTs the session-specific revoke endpoint and carries '
        'the no-auto-retry extra', () async {
      when(() => adapter.fetch(any(), any(), any()))
          .thenAnswer((_) async => jsonBody('', 204));

      await api.revokeSession('s1', {AuthInterceptor.noAutoRetryKey: true});

      final options = verify(() => adapter.fetch(captureAny(), any(), any()))
          .captured
          .single as RequestOptions;
      expect(options.method, 'POST');
      expect(options.path, 'me/sessions/s1/revoke');
      expect(options.queryParameters, isEmpty);
      // Non-idempotent mutation must never be transparently refresh+retried.
      expect(options.extra[AuthInterceptor.noAutoRetryKey], isTrue);
      // Nothing but the path id travels: no payload, no member/account id.
      expect(options.data, isNull);
      expect(options.path.contains('member'), isFalse);
      expect(options.path.contains('account'), isFalse);
    });

    test('an adapter 404 propagates and is never fabricated as success',
        () async {
      when(() => adapter.fetch(any(), any(), any())).thenThrow(
        DioException(
          requestOptions: RequestOptions(path: 'me/sessions/s1/revoke'),
          type: DioExceptionType.badResponse,
          response: Response<dynamic>(
            requestOptions: RequestOptions(path: 'me/sessions/s1/revoke'),
            statusCode: 404,
          ),
        ),
      );

      expect(
        api.revokeSession('s1', null),
        throwsA(isA<DioException>()),
      );
    });

    test(
        'revokeOtherSessions POSTs the revoke-others endpoint with no '
        'auto-retry and no payload or identity', () async {
      when(() => adapter.fetch(any(), any(), any()))
          .thenAnswer((_) async => jsonBody('', 204));

      await api.revokeOtherSessions({AuthInterceptor.noAutoRetryKey: true});

      final options = verify(() => adapter.fetch(captureAny(), any(), any()))
          .captured
          .single as RequestOptions;
      expect(options.method, 'POST');
      expect(options.path, 'me/sessions/revoke-others');
      expect(options.queryParameters, isEmpty);
      // Non-idempotent mutation must never be transparently refresh+retried.
      expect(options.extra[AuthInterceptor.noAutoRetryKey], isTrue);
      // The backend derives the actor from the token and the current logical
      // session from the signed sid claim: no body and no identifiers travel.
      expect(options.data, isNull);
      expect(options.path.contains('member'), isFalse);
      expect(options.path.contains('account'), isFalse);
      expect(options.path.contains('{'), isFalse);
    });

    test('an adapter 401 on revokeOtherSessions propagates as DioException',
        () async {
      when(() => adapter.fetch(any(), any(), any())).thenThrow(
        DioException(
          requestOptions: RequestOptions(path: 'me/sessions/revoke-others'),
          type: DioExceptionType.badResponse,
          response: Response<dynamic>(
            requestOptions: RequestOptions(path: 'me/sessions/revoke-others'),
            statusCode: 401,
          ),
        ),
      );

      expect(
        api.revokeOtherSessions(null),
        throwsA(isA<DioException>()),
      );
    });

    test(
        'removeMfaMethod DELETEs the method-specific endpoint and carries '
        'the no-auto-retry extra', () async {
      when(() => adapter.fetch(any(), any(), any()))
          .thenAnswer((_) async => jsonBody('', 204));

      await api.removeMfaMethod('m1', {AuthInterceptor.noAutoRetryKey: true});

      final options = verify(() => adapter.fetch(captureAny(), any(), any()))
          .captured
          .single as RequestOptions;
      expect(options.method, 'DELETE');
      expect(options.path, 'mfa/m1');
      expect(options.queryParameters, isEmpty);
      // Non-idempotent mutation must never be transparently refresh+retried.
      expect(options.extra[AuthInterceptor.noAutoRetryKey], isTrue);
      // Nothing but the path id travels: no payload, no member/account id.
      expect(options.data, isNull);
      expect(options.path.contains('member'), isFalse);
      expect(options.path.contains('account'), isFalse);
    });

    test('an adapter 404 on removeMfaMethod propagates as DioException',
        () async {
      when(() => adapter.fetch(any(), any(), any())).thenThrow(
        DioException(
          requestOptions: RequestOptions(path: 'mfa/m1'),
          type: DioExceptionType.badResponse,
          response: Response<dynamic>(
            requestOptions: RequestOptions(path: 'mfa/m1'),
            statusCode: 404,
          ),
        ),
      );

      expect(
        api.removeMfaMethod('m1', null),
        throwsA(isA<DioException>()),
      );
    });

    test('an adapter 409 on removeMfaMethod propagates as DioException',
        () async {
      when(() => adapter.fetch(any(), any(), any())).thenThrow(
        DioException(
          requestOptions: RequestOptions(path: 'mfa/m1'),
          type: DioExceptionType.badResponse,
          response: Response<dynamic>(
            requestOptions: RequestOptions(path: 'mfa/m1'),
            statusCode: 409,
          ),
        ),
      );

      expect(
        api.removeMfaMethod('m1', null),
        throwsA(isA<DioException>()),
      );
    });
  });
}
