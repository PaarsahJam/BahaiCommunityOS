import 'package:dio/dio.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:member_portal/core/network/auth_interceptor.dart';
import 'package:member_portal/features/notifications/data/notifications_api.dart';
import 'package:mocktail/mocktail.dart';

class _MockAdapter extends Mock implements HttpClientAdapter {}

void main() {
  group('NotificationApi (wire contract)', () {
    late _MockAdapter adapter;
    late NotificationApi api;

    setUpAll(() {
      registerFallbackValue(RequestOptions(path: 'fallback'));
    });

    setUp(() {
      adapter = _MockAdapter();
      final dio = Dio(BaseOptions(baseUrl: 'http://api.test'));
      dio.httpClientAdapter = adapter;
      api = NotificationApi(dio);
    });

    ResponseBody jsonBody(String body) => ResponseBody.fromString(
          body,
          200,
          headers: {
            Headers.contentTypeHeader: [Headers.jsonContentType]
          },
        );

    test(
        'myNotifications GETs the member surface with bounded paging and no '
        'member identifier', () async {
      when(() => adapter.fetch(any(), any(), any())).thenAnswer(
        (_) async => jsonBody('''
          [{
            "id": "n1",
            "typeCode": "community-event",
            "channel": "InApp",
            "status": "Sent",
            "title": "Study circle",
            "body": "Starts on Saturday.",
            "isRead": false,
            "readAt": null,
            "createdOn": "2026-09-01T12:00:00Z"
          }]
        '''),
      );

      final items = await api.myNotifications(limit: 50, offset: 20);

      final options = verify(() => adapter.fetch(captureAny(), any(), any()))
          .captured
          .single as RequestOptions;
      expect(options.method, 'GET');
      expect(options.path, 'my-notifications');
      expect(options.queryParameters['limit'], 50);
      expect(options.queryParameters['offset'], 20);
      // No member identifier is ever part of the request.
      expect(options.path.contains('member'), isFalse);
      expect(options.queryParameters.keys.any((k) => k.contains('member')),
          isFalse);

      final item = items.single;
      expect(item.id, 'n1');
      expect(item.typeCode, 'community-event');
      expect(item.channel, 'InApp');
      expect(item.status, 'Sent');
      expect(item.title, 'Study circle');
      expect(item.body, 'Starts on Saturday.');
      expect(item.isRead, isFalse);
      expect(item.readAt, isNull);
      expect(item.createdOn, DateTime.parse('2026-09-01T12:00:00Z'));
    });

    test('unreadNotificationCount GETs the authoritative count endpoint',
        () async {
      when(() => adapter.fetch(any(), any(), any())).thenAnswer(
        (_) async => jsonBody('{"count": 3}'),
      );

      final count = await api.unreadNotificationCount();

      final options = verify(() => adapter.fetch(captureAny(), any(), any()))
          .captured
          .single as RequestOptions;
      expect(options.method, 'GET');
      expect(options.path, 'my-notifications/unread-count');
      expect(count.count, 3);
    });

    test(
        'markNotificationRead POSTs the read action with the no-auto-retry '
        'extra and returns the authoritative updated notification', () async {
      when(() => adapter.fetch(any(), any(), any())).thenAnswer(
        (_) async => jsonBody('''
          {
            "id": "n1",
            "typeCode": "community-event",
            "channel": "InApp",
            "status": "Sent",
            "title": "Study circle",
            "body": "Starts on Saturday.",
            "isRead": true,
            "readAt": "2026-09-07T09:00:00Z",
            "createdOn": "2026-09-01T12:00:00Z"
          }
        '''),
      );

      final updated = await api.markNotificationRead('n1', {
        AuthInterceptor.noAutoRetryKey: true,
      });

      final options = verify(() => adapter.fetch(captureAny(), any(), any()))
          .captured
          .single as RequestOptions;
      expect(options.method, 'POST');
      expect(options.path, 'my-notifications/n1/read');
      // Non-idempotent mutation must never be transparently refresh+retried.
      expect(options.extra[AuthInterceptor.noAutoRetryKey], isTrue);
      expect(updated.isRead, isTrue);
      expect(updated.readAt, DateTime.parse('2026-09-07T09:00:00Z'));
    });

    test('throws whenever the adapter errors (never fabricates data)',
        () async {
      when(() => adapter.fetch(any(), any(), any())).thenThrow(
        DioException(
          requestOptions: RequestOptions(path: '/my-notifications'),
          type: DioExceptionType.connectionError,
        ),
      );

      expect(api.myNotifications(), throwsA(isA<DioException>()));
    });
  });
}
