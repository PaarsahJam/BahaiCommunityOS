import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:member_portal/core/error/app_exception.dart';
import 'package:member_portal/features/notifications/application/notification_bloc.dart';
import 'package:member_portal/features/notifications/data/notifications_dtos.dart';
import 'package:member_portal/features/notifications/domain/notification_repository.dart';
import 'package:member_portal/features/notifications/presentation/notification_center_page.dart';
import 'package:member_portal/l10n/generated/app_localizations.dart';
import 'package:mocktail/mocktail.dart';

class _MockNotificationRepository extends Mock
    implements NotificationRepository {}

void main() {
  late _MockNotificationRepository repository;

  setUp(() {
    repository = _MockNotificationRepository();
  });

  MemberNotificationSummaryDto dto({
    required String id,
    String typeCode = 'community-event',
    bool isRead = false,
    DateTime? readAt,
  }) =>
      MemberNotificationSummaryDto(
        id: id,
        typeCode: typeCode,
        channel: 'InApp',
        status: 'Sent',
        title: 'Study circle $id',
        body: 'Bring a friend.',
        isRead: isRead,
        readAt: readAt,
        createdOn: DateTime(2026, 9, 1, 12),
      );

  List<MemberNotificationSummaryDto> items([int count = 2]) =>
      [for (var i = 0; i < count; i++) dto(id: 'n$i')];

  Widget harness(Widget child) => MaterialApp(
        theme: ThemeData(useMaterial3: true),
        localizationsDelegates: AppLocalizations.localizationsDelegates,
        supportedLocales: AppLocalizations.supportedLocales,
        home: child,
      );

  Future<void> pumpFrames(WidgetTester tester, [int count = 20]) async {
    for (var i = 0; i < count; i++) {
      await tester.pump(const Duration(milliseconds: 100));
    }
  }

  testWidgets('shows a spinner while loading, then the authoritative content',
      (tester) async {
    final firstPage = Completer<List<MemberNotificationSummaryDto>>();
    when(() => repository.myNotifications(limit: 50, offset: 0))
        .thenAnswer((_) => firstPage.future);
    when(() => repository.unreadCount()).thenAnswer((_) async => 1);

    await tester.pumpWidget(harness(
      NotificationCenterPage(createBloc: () => NotificationBloc(repository)),
    ));
    await tester.pump();

    expect(find.byType(CircularProgressIndicator), findsOneWidget);

    firstPage.complete(items(2));
    await pumpFrames(tester);

    expect(find.byType(NotificationCenterPage), findsOneWidget);
    // server content is rendered verbatim (title + body)
    expect(find.text('Study circle n0'), findsOneWidget);
    expect(find.text('Bring a friend.'), findsNWidgets(2));
    // localized type label and explicit read/unread labels
    expect(find.text('Community event'), findsNWidgets(2));
    expect(find.text('Unread'), findsNWidgets(2));
    // authoritative unread summary footer
    expect(find.text('You have 1 unread notification(s).'), findsOneWidget);
    // each unread card offers the explicit mark-as-read action
    expect(find.byKey(const Key('mark-read')), findsNWidgets(2));
  });

  testWidgets('mark-as-read transitions only through the server response',
      (tester) async {
    when(() => repository.myNotifications(limit: 50, offset: 0))
        .thenAnswer((_) async => items(1));
    when(() => repository.unreadCount()).thenAnswer((_) async => 1);
    when(() => repository.markRead('n0')).thenAnswer(
      (_) async => dto(id: 'n0', isRead: true, readAt: DateTime(2026, 9, 7)),
    );

    await tester.pumpWidget(harness(
      NotificationCenterPage(createBloc: () => NotificationBloc(repository)),
    ));
    await pumpFrames(tester);

    expect(find.text('Unread'), findsOneWidget);
    await tester.tap(find.byKey(const Key('mark-read')));
    await pumpFrames(tester);

    verify(() => repository.markRead('n0')).called(1);
    expect(find.text('Read'), findsOneWidget);
    expect(find.text('Unread'), findsNothing);
    expect(find.byKey(const Key('mark-read')), findsNothing);
    // read state reflects server-provided readAt via the chip
    expect(find.textContaining('Received'), findsOneWidget);
  });

  testWidgets('an empty response renders the empty state, never a fake list',
      (tester) async {
    when(() => repository.myNotifications(limit: 50, offset: 0))
        .thenAnswer((_) async => <MemberNotificationSummaryDto>[]);
    when(() => repository.unreadCount()).thenAnswer((_) async => 0);

    await tester.pumpWidget(harness(
      NotificationCenterPage(createBloc: () => NotificationBloc(repository)),
    ));
    await pumpFrames(tester);

    expect(find.text('You have no notifications.'), findsOneWidget);
    expect(find.byKey(const Key('notification-list')), findsNothing);
  });

  testWidgets('a failed list request shows the error view, not an empty list',
      (tester) async {
    when(() => repository.myNotifications(limit: 50, offset: 0)).thenThrow(
      const NetworkException('api.example.com refused the connection'),
    );
    when(() => repository.unreadCount()).thenAnswer((_) async => 0);

    await tester.pumpWidget(harness(
      NotificationCenterPage(createBloc: () => NotificationBloc(repository)),
    ));
    await pumpFrames(tester);

    expect(find.text('You have no notifications.'), findsNothing);
    expect(find.byType(CircularProgressIndicator), findsNothing);
    expect(find.text('Refresh'), findsWidgets);

    // retry performs a fresh authoritative request
    when(() => repository.myNotifications(limit: 50, offset: 0))
        .thenAnswer((_) async => items(1));
    await tester.tap(find.text('Refresh').last);
    await pumpFrames(tester);
    expect(find.text('Study circle n0'), findsOneWidget);
  });

  testWidgets('mark-as-read failure is localized and keeps the item unread',
      (tester) async {
    when(() => repository.myNotifications(limit: 50, offset: 0))
        .thenAnswer((_) async => items(1));
    when(() => repository.unreadCount()).thenAnswer((_) async => 1);
    when(() => repository.markRead('n0')).thenThrow(const ServerException('x'));

    await tester.pumpWidget(harness(
      NotificationCenterPage(createBloc: () => NotificationBloc(repository)),
    ));
    await pumpFrames(tester);

    await tester.tap(find.byKey(const Key('mark-read')));
    await pumpFrames(tester);

    expect(find.text('Unread'), findsOneWidget);
    expect(
        find.text('Could not mark as read. Please try again.'), findsOneWidget);
    // the mark-as-read control remains available to retry
    expect(find.byKey(const Key('mark-read')), findsOneWidget);
  });

  testWidgets('bounded pagination: Load more appends the next page',
      (tester) async {
    final first = [
      for (var i = 0; i < NotificationBloc.pageSize; i++) dto(id: 'n$i')
    ];
    when(() => repository.myNotifications(limit: 50, offset: 0))
        .thenAnswer((_) async => first);
    when(() => repository.myNotifications(
            limit: 50, offset: NotificationBloc.pageSize))
        .thenAnswer((_) async => [dto(id: 'n${NotificationBloc.pageSize}')]);
    when(() => repository.unreadCount()).thenAnswer((_) async => 0);

    await tester.pumpWidget(harness(
      NotificationCenterPage(createBloc: () => NotificationBloc(repository)),
    ));
    await pumpFrames(tester);

    final listFinder = find.byKey(const Key('notification-list'));
    final loadMore = find.byKey(const Key('load-more'));
    final scrollable =
        find.descendant(of: listFinder, matching: find.byType(Scrollable));
    await tester.scrollUntilVisible(loadMore, 300, scrollable: scrollable);
    expect(loadMore, findsOneWidget);

    await tester.tap(loadMore);
    await pumpFrames(tester);

    // next page appended: n50 (page 2) now present; a second page that is
    // shorter than the limit ends pagination
    final appended = find.text('Study circle n${NotificationBloc.pageSize}');
    await tester.scrollUntilVisible(appended, 300, scrollable: scrollable);
    expect(appended, findsOneWidget);
    expect(find.byKey(const Key('load-more')), findsNothing);
  });
}
