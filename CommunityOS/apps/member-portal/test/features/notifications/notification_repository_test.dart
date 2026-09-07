import 'package:dio/dio.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:member_portal/core/error/app_exception.dart';
import 'package:member_portal/core/network/auth_interceptor.dart';
import 'package:member_portal/core/network/error_mapper.dart';
import 'package:member_portal/features/notifications/data/notifications_api.dart';
import 'package:member_portal/features/notifications/data/notifications_dtos.dart';
import 'package:member_portal/features/notifications/domain/notification_repository.dart';
import 'package:mocktail/mocktail.dart';

class _MockNotificationApi extends Mock implements NotificationApi {}

void main() {
  late _MockNotificationApi api;
  late NotificationRepository repository;

  setUp(() {
    api = _MockNotificationApi();
    repository = NotificationRepository(api, const ErrorMapper());
  });

  MemberNotificationSummaryDto dto({required String id, bool isRead = false}) =>
      MemberNotificationSummaryDto(
        id: id,
        typeCode: 'general',
        channel: 'InApp',
        status: 'Sent',
        title: 'Hello',
        body: 'Body',
        isRead: isRead,
        createdOn: DateTime(2026, 9, 1),
      );

  group('myNotifications', () {
    test('passes the bounded page to the backend and returns its items',
        () async {
      final items = [dto(id: 'n1')];
      when(() => api.myNotifications(limit: 25, offset: 100))
          .thenAnswer((_) async => items);

      final result = await repository.myNotifications(limit: 25, offset: 100);

      expect(result, same(items));
      verify(() => api.myNotifications(limit: 25, offset: 100)).called(1);
    });

    test('maps a transport failure to a NetworkException', () async {
      when(() => api.myNotifications(limit: 50, offset: 0)).thenThrow(
        DioException(
          requestOptions: RequestOptions(path: '/my-notifications'),
          type: DioExceptionType.connectionError,
        ),
      );

      expect(
        () => repository.myNotifications(),
        throwsA(isA<NetworkException>()),
      );
    });
  });

  group('unreadCount', () {
    test('returns the authoritative count from the dedicated endpoint',
        () async {
      when(() => api.unreadNotificationCount())
          .thenAnswer((_) async => const MemberUnreadCountDto(count: 5));

      expect(await repository.unreadCount(), 5);
    });

    test('maps a 409/validation failure to a ValidationException', () async {
      when(() => api.unreadNotificationCount()).thenThrow(
        DioException(
          requestOptions:
              RequestOptions(path: '/my-notifications/unread-count'),
          type: DioExceptionType.badResponse,
          response: Response(
            requestOptions:
                RequestOptions(path: '/my-notifications/unread-count'),
            statusCode: 422,
            data: {'detail': 'nope'},
          ),
        ),
      );

      expect(
        () => repository.unreadCount(),
        throwsA(isA<ValidationException>()),
      );
    });
  });

  group('markRead', () {
    test('marks the exact notification read via POST with no-auto-retry',
        () async {
      final updated =
          dto(id: 'n1', isRead: true).copyWith(readAt: DateTime(2026, 9, 7));
      when(() => api.markNotificationRead('n1', any())).thenAnswer(
        (invocation) async {
          final extra =
              invocation.positionalArguments[1] as Map<String, dynamic>;
          expect(extra[AuthInterceptor.noAutoRetryKey], isTrue);
          return updated;
        },
      );

      final result = await repository.markRead('n1');

      // The exact recipient-scoped id is the only identifier ever supplied.
      verify(() => api.markNotificationRead('n1', any())).called(1);
      expect(result.isRead, isTrue);
      expect(result.readAt, DateTime(2026, 9, 7));
    });

    test('maps the backend rejection verbatim without retrying', () async {
      when(() => api.markNotificationRead('n1', any())).thenThrow(
        DioException(
          requestOptions: RequestOptions(path: '/my-notifications/n1/read'),
          type: DioExceptionType.badResponse,
          response: Response(
            requestOptions: RequestOptions(path: '/my-notifications/n1/read'),
            statusCode: 409,
            data: {'detail': 'conflict'},
          ),
        ),
      );

      expect(
          () => repository.markRead('n1'), throwsA(isA<ValidationException>()));
    });
  });
}
