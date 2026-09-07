import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:member_portal/core/error/app_exception.dart';
import 'package:member_portal/features/notifications/application/home_unread_bloc.dart';
import 'package:member_portal/features/notifications/domain/notification_repository.dart';
import 'package:member_portal/features/notifications/presentation/widgets/home_notifications_badge.dart';
import 'package:member_portal/l10n/generated/app_localizations.dart';
import 'package:mocktail/mocktail.dart';

class _MockNotificationRepository extends Mock
    implements NotificationRepository {}

void main() {
  late _MockNotificationRepository repository;

  setUp(() {
    repository = _MockNotificationRepository();
  });

  Widget harness(Widget child) => MaterialApp(
        theme: ThemeData(useMaterial3: true),
        localizationsDelegates: AppLocalizations.localizationsDelegates,
        supportedLocales: AppLocalizations.supportedLocales,
        home: Scaffold(
          appBar: AppBar(
            actions: [child],
          ),
        ),
      );

  Widget badge() => HomeNotificationsBadge(
        createBloc: () => HomeUnreadBloc(repository),
      );

  Future<void> pumpFrames(WidgetTester tester, [int count = 20]) async {
    for (var i = 0; i < count; i++) {
      await tester.pump(const Duration(milliseconds: 100));
    }
  }

  testWidgets('renders no numeric badge while the authoritative count is 0',
      (tester) async {
    when(() => repository.unreadCount()).thenAnswer((_) async => 0);

    await tester.pumpWidget(harness(badge()));
    await pumpFrames(tester);

    expect(find.byIcon(Icons.notifications_outlined), findsOneWidget);
    expect(find.bySemanticsLabel(RegExp(r'\d unread notifications')),
        findsNothing);
  });

  testWidgets('shows the authoritative count when the server reports it',
      (tester) async {
    when(() => repository.unreadCount()).thenAnswer((_) async => 3);

    await tester.pumpWidget(harness(badge()));
    await pumpFrames(tester);

    expect(find.bySemanticsLabel('3 unread notifications'), findsOneWidget);
  });

  testWidgets('caps the badge digits at 99+ while annotating the true count',
      (tester) async {
    when(() => repository.unreadCount()).thenAnswer((_) async => 150);

    await tester.pumpWidget(harness(badge()));
    await pumpFrames(tester);

    // semantics exposes the true value, never the clipped rendering
    expect(find.bySemanticsLabel('150 unread notifications'), findsOneWidget);
  });

  testWidgets('a failed count is never rendered as a zero badge',
      (tester) async {
    when(() => repository.unreadCount())
        .thenThrow(const NetworkException('down'));

    await tester.pumpWidget(harness(badge()));
    await pumpFrames(tester);

    expect(find.byIcon(Icons.notifications_outlined), findsOneWidget);
    expect(find.bySemanticsLabel(RegExp(r'\d unread notifications')),
        findsNothing);
  });

  testWidgets('without a registered repository it degrades to the bare icon',
      (tester) async {
    // No get_it registration, no injected bloc, no configured repository:
    // the badge must still render without fabricating a count or crashing.
    await tester.pumpWidget(harness(const HomeNotificationsBadge()));
    await pumpFrames(tester);

    expect(find.byIcon(Icons.notifications_outlined), findsOneWidget);
    expect(find.bySemanticsLabel(RegExp(r'\d unread notifications')),
        findsNothing);
  });
}
