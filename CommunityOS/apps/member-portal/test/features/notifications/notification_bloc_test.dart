import 'package:flutter_test/flutter_test.dart';
import 'package:member_portal/core/error/app_exception.dart';
import 'package:member_portal/features/notifications/application/notification_bloc.dart';
import 'package:member_portal/features/notifications/application/notification_event.dart';
import 'package:member_portal/features/notifications/application/notification_state.dart';
import 'package:member_portal/features/notifications/data/notifications_dtos.dart';
import 'package:member_portal/features/notifications/domain/notification_repository.dart';
import 'package:mocktail/mocktail.dart';

class _MockNotificationRepository extends Mock
    implements NotificationRepository {}

Future<void> _flush() async {
  await pumpEventQueue();
  await Future<void>.delayed(Duration.zero);
  await pumpEventQueue();
}

void main() {
  late _MockNotificationRepository repository;

  setUp(() {
    repository = _MockNotificationRepository();
  });

  MemberNotificationSummaryDto dto({
    required String id,
    bool isRead = false,
    DateTime? readAt,
  }) =>
      MemberNotificationSummaryDto(
        id: id,
        typeCode: 'community-event',
        channel: 'InApp',
        status: 'Sent',
        title: 'Study circle $id',
        body: 'Bring a friend.',
        isRead: isRead,
        readAt: readAt,
        createdOn: DateTime(2026, 9, 1, 12),
      );

  List<MemberNotificationSummaryDto> page(int count, {int start = 0}) =>
      [for (var i = 0; i < count; i++) dto(id: 'n${start + i}')];

  group('NotificationBloc', () {
    test('loads the first page and the authoritative unread count', () async {
      final items = page(2);
      when(() => repository.myNotifications(limit: 50, offset: 0))
          .thenAnswer((_) async => items);
      when(() => repository.unreadCount()).thenAnswer((_) async => 2);

      final bloc = NotificationBloc(repository);
      addTearDown(bloc.close);
      final states = <NotificationState>[];
      final sub = bloc.stream.listen(states.add);
      bloc.add(const NotificationEvent.requested());
      await _flush();

      expect(states, contains(isA<NotificationLoading>()));
      final loaded = states.last;
      expect(loaded, isA<NotificationLoaded>());
      // a full first page leaves more pages possible
      expect((loaded as NotificationLoaded).items, hasLength(2));
      expect(loaded.hasMore, isFalse);
      expect(loaded.unreadCount, 2);
      expect(loaded.unreadCountError, isNull);
      await sub.cancel();
    });

    test('a full first page signals more pages may exist', () async {
      final full = page(NotificationBloc.pageSize);
      when(() => repository.myNotifications(limit: 50, offset: 0))
          .thenAnswer((_) async => full);
      when(() => repository.unreadCount()).thenAnswer((_) async => 0);

      final bloc = NotificationBloc(repository);
      addTearDown(bloc.close);
      final states = <NotificationState>[];
      final sub = bloc.stream.listen(states.add);
      bloc.add(const NotificationEvent.requested());
      await _flush();

      expect((states.last as NotificationLoaded).hasMore, isTrue);
      await sub.cancel();
    });

    test('a failed first page is surfaced as failed, never as empty', () async {
      when(() => repository.myNotifications(limit: 50, offset: 0))
          .thenThrow(const NetworkException('down'));
      when(() => repository.unreadCount()).thenAnswer((_) async => 0);

      final bloc = NotificationBloc(repository);
      addTearDown(bloc.close);
      final states = <NotificationState>[];
      final sub = bloc.stream.listen(states.add);
      bloc.add(const NotificationEvent.requested());
      await _flush();

      final failed = states.last;
      expect(failed, isA<NotificationFailed>());
      expect((failed as NotificationFailed).error, isA<NetworkException>());
      await sub.cancel();
    });

    test('loadMore appends a page and stops at the terminal page', () async {
      final first = page(NotificationBloc.pageSize);
      final second = page(2, start: NotificationBloc.pageSize);
      var secondCalls = 0;
      when(() => repository.myNotifications(limit: 50, offset: 0))
          .thenAnswer((_) async => first);
      when(() => repository.myNotifications(
          limit: 50, offset: NotificationBloc.pageSize)).thenAnswer((_) async {
        secondCalls++;
        return second;
      });
      when(() => repository.unreadCount()).thenAnswer((_) async => 0);

      final bloc = NotificationBloc(repository);
      addTearDown(bloc.close);
      final states = <NotificationState>[];
      final sub = bloc.stream.listen(states.add);
      bloc.add(const NotificationEvent.requested());
      await _flush();
      bloc.add(const NotificationEvent.loadMoreRequested());
      // A second load-more while the first is still in flight is suppressed.
      bloc.add(const NotificationEvent.loadMoreRequested());
      await _flush();

      expect(secondCalls, 1);
      final loaded = states.last as NotificationLoaded;
      expect(loaded.items, hasLength(NotificationBloc.pageSize + 2));
      expect(loaded.items.last.id, 'n${NotificationBloc.pageSize + 1}');
      expect(loaded.isLoadingMore, isFalse);
      // second page was shorter than the limit: no more pages
      expect(loaded.hasMore, isFalse);
      await sub.cancel();
    });

    test('never pages past the terminal page', () async {
      final first = page(2);
      var pageOneCalls = 0;
      when(() => repository.myNotifications(limit: 50, offset: 0))
          .thenAnswer((_) async {
        pageOneCalls++;
        return first;
      });
      when(() => repository.myNotifications(limit: 50, offset: 2))
          .thenAnswer((_) async => <MemberNotificationSummaryDto>[]);
      when(() => repository.unreadCount()).thenAnswer((_) async => 0);

      final bloc = NotificationBloc(repository);
      addTearDown(bloc.close);
      final states = <NotificationState>[];
      final sub = bloc.stream.listen(states.add);
      bloc.add(const NotificationEvent.requested());
      await _flush();
      bloc.add(const NotificationEvent.loadMoreRequested());
      await _flush();
      // terminal reached: further loads are rejected
      bloc.add(const NotificationEvent.loadMoreRequested());
      await _flush();

      expect(pageOneCalls, 1);
      // terminal reached: the second page is never requested
      verifyNever(() => repository.myNotifications(limit: 50, offset: 2));
      expect((states.last as NotificationLoaded).items, hasLength(2));
      await sub.cancel();
    });

    test('a failed additional page keeps the list and surfaces a load error',
        () async {
      final first = page(NotificationBloc.pageSize);
      when(() => repository.myNotifications(limit: 50, offset: 0))
          .thenAnswer((_) async => first);
      when(() => repository.myNotifications(
              limit: 50, offset: NotificationBloc.pageSize))
          .thenThrow(const ServerException('boom'));
      when(() => repository.unreadCount()).thenAnswer((_) async => 0);

      final bloc = NotificationBloc(repository);
      addTearDown(bloc.close);
      final states = <NotificationState>[];
      final sub = bloc.stream.listen(states.add);
      bloc.add(const NotificationEvent.requested());
      await _flush();
      bloc.add(const NotificationEvent.loadMoreRequested());
      await _flush();

      final loaded = states.last as NotificationLoaded;
      expect(loaded.items, hasLength(NotificationBloc.pageSize));
      expect(loaded.isLoadingMore, isFalse);
      expect(loaded.loadMoreError, isA<ServerException>());
      await sub.cancel();
    });

    test(
        'markRead replaces the item with the authoritative response and '
        'refreshes the count', () async {
      final item = dto(id: 'n1');
      final read = dto(id: 'n1', isRead: true, readAt: DateTime(2026, 9, 7));
      when(() => repository.myNotifications(limit: 50, offset: 0))
          .thenAnswer((_) async => [item]);
      when(() => repository.unreadCount()).thenAnswer((_) async => 1);
      when(() => repository.markRead('n1')).thenAnswer((_) async => read);

      final bloc = NotificationBloc(repository);
      addTearDown(bloc.close);
      final states = <NotificationState>[];
      final sub = bloc.stream.listen(states.add);
      bloc.add(const NotificationEvent.requested());
      await _flush();
      bloc.add(const NotificationEvent.markReadRequested(id: 'n1'));
      await _flush();

      final loaded = states.last as NotificationLoaded;
      expect(loaded.items.single.isRead, isTrue);
      expect(loaded.items.single.readAt, DateTime(2026, 9, 7));
      expect(loaded.markReadStatuses['n1'], isA<MarkReadSucceeded>());
      // the count is refreshed from the authoritative endpoint
      expect(loaded.unreadCount, 1);
      verify(() => repository.unreadCount()).called(2);
      await sub.cancel();
    });

    test('a failed markRead keeps the item unread and allows retry', () async {
      final item = dto(id: 'n1');
      var markReadCalls = 0;
      when(() => repository.myNotifications(limit: 50, offset: 0))
          .thenAnswer((_) async => [item]);
      when(() => repository.unreadCount()).thenAnswer((_) async => 1);
      when(() => repository.markRead('n1')).thenAnswer((_) async {
        markReadCalls++;
        if (markReadCalls == 1) {
          throw const ServerException('boom');
        }
        return dto(id: 'n1', isRead: true, readAt: DateTime(2026, 9, 7));
      });

      final bloc = NotificationBloc(repository);
      addTearDown(bloc.close);
      final states = <NotificationState>[];
      final sub = bloc.stream.listen(states.add);
      bloc.add(const NotificationEvent.requested());
      await _flush();
      bloc.add(const NotificationEvent.markReadRequested(id: 'n1'));
      await _flush();

      var loaded = states.last as NotificationLoaded;
      expect(loaded.items.single.isRead, isFalse);
      expect(loaded.markReadStatuses['n1'], isA<MarkReadFailed>());

      bloc.add(const NotificationEvent.markReadRequested(id: 'n1'));
      await _flush();

      loaded = states.last as NotificationLoaded;
      expect(loaded.items.single.isRead, isTrue);
      expect(loaded.markReadStatuses['n1'], isA<MarkReadSucceeded>());
      expect(markReadCalls, 2);
      await sub.cancel();
    });

    test('already-read notifications are never re-submitted', () async {
      final read = dto(id: 'n1', isRead: true, readAt: DateTime(2026, 9, 7));
      when(() => repository.myNotifications(limit: 50, offset: 0))
          .thenAnswer((_) async => [read]);
      when(() => repository.unreadCount()).thenAnswer((_) async => 0);

      final bloc = NotificationBloc(repository);
      addTearDown(bloc.close);
      bloc.add(const NotificationEvent.requested());
      await _flush();
      bloc.add(const NotificationEvent.markReadRequested(id: 'n1'));
      await _flush();

      verifyNever(() => repository.markRead('n1'));
    });

    test('a failed unread count is surfaced and never zeroed', () async {
      when(() => repository.myNotifications(limit: 50, offset: 0))
          .thenAnswer((_) async => [dto(id: 'n1')]);
      when(() => repository.unreadCount())
          .thenThrow(const NetworkException('down'));

      final bloc = NotificationBloc(repository);
      addTearDown(bloc.close);
      final states = <NotificationState>[];
      final sub = bloc.stream.listen(states.add);
      bloc.add(const NotificationEvent.requested());
      await _flush();

      final loaded = states.last as NotificationLoaded;
      expect(loaded.unreadCount, isNull);
      expect(loaded.unreadCountError, isA<NetworkException>());
      await sub.cancel();
    });

    test('never emits after the bloc has been closed', () async {
      when(() => repository.myNotifications(limit: 50, offset: 0))
          .thenAnswer((_) async {
        // keep the first page pending
        await Future<void>.delayed(const Duration(milliseconds: 50));
        return page(1);
      });

      final bloc = NotificationBloc(repository);
      final states = <NotificationState>[];
      final sub = bloc.stream.listen(states.add);
      bloc.add(const NotificationEvent.requested());
      await _flush();
      expect(states.last, isA<NotificationLoading>());
      bloc.close();
      await Future<void>.delayed(const Duration(milliseconds: 100));
      // no further emissions after close
      expect(states.last, isA<NotificationLoading>());
      await sub.cancel();
    });
  });
}
