import 'package:flutter/material.dart';
import 'package:flutter_bloc/flutter_bloc.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:go_router/go_router.dart';
import 'package:member_portal/core/network/refresh_coordinator.dart';
import 'package:member_portal/core/router/app_router.dart';
import 'package:member_portal/features/auth/application/auth_bloc.dart';
import 'package:member_portal/features/auth/application/auth_event.dart';
import 'package:member_portal/features/auth/domain/auth_models.dart';
import 'package:member_portal/features/auth/domain/auth_repository.dart';
import 'package:member_portal/features/auth/presentation/login_page.dart';
import 'package:member_portal/features/auth/presentation/mfa_page.dart';
import 'package:member_portal/features/member/application/member_session_bloc.dart';
import 'package:member_portal/features/member/application/membership_bloc.dart';
import 'package:member_portal/features/member/data/member_dtos.dart';
import 'package:member_portal/features/member/domain/member_models.dart';
import 'package:member_portal/features/member/domain/member_repository.dart';
import 'package:member_portal/features/member/presentation/home_page.dart';
import 'package:member_portal/features/notifications/application/notification_bloc.dart';
import 'package:member_portal/features/notifications/data/notifications_dtos.dart';
import 'package:member_portal/features/notifications/domain/notification_repository.dart';
import 'package:member_portal/features/notifications/presentation/notification_center_page.dart';
import 'package:member_portal/l10n/generated/app_localizations.dart';
import 'package:mocktail/mocktail.dart';

class _MockAuthRepository extends Mock implements AuthRepository {}

class _MockCoordinator extends Mock implements RefreshCoordinator {}

class _MockMemberRepository extends Mock implements MemberRepository {}

class _MockNotificationRepository extends Mock
    implements NotificationRepository {}

void main() {
  late _MockAuthRepository authRepo;
  late _MockCoordinator coordinator;
  late _MockMemberRepository memberRepo;
  late _MockNotificationRepository notifRepo;

  setUp(() {
    authRepo = _MockAuthRepository();
    coordinator = _MockCoordinator();
    when(() => coordinator.onSessionExpired)
        .thenAnswer((_) => const Stream.empty());
    memberRepo = _MockMemberRepository();
    when(() => memberRepo.getMembership(any())).thenAnswer((_) async => null);
    notifRepo = _MockNotificationRepository();
  });

  Future<void> pumpFrames(WidgetTester tester, [int count = 30]) async {
    for (var i = 0; i < count; i++) {
      await tester.pump(const Duration(milliseconds: 100));
    }
  }

  Widget harness(AuthBloc authBloc, GoRouter router) {
    return BlocProvider<AuthBloc>.value(
      value: authBloc,
      child: MaterialApp.router(
        theme: ThemeData(useMaterial3: true),
        routerConfig: router,
        localizationsDelegates: AppLocalizations.localizationsDelegates,
        supportedLocales: AppLocalizations.supportedLocales,
      ),
    );
  }

  GoRouter buildRouter(AuthBloc authBloc) => AppRouter.build(
        authBloc,
        createMemberSession: () => MemberSessionBloc(memberRepo),
        createMembership: () => MembershipBloc(memberRepo),
        createNotifications: () => NotificationBloc(notifRepo),
      );

  void stubSignIn({required String email}) {
    when(
      () => authRepo.login(
        email: any(named: 'email'),
        password: any(named: 'password'),
      ),
    ).thenAnswer(
      (_) async => AuthLoginResult.requiresMfa(
        accountId: email == 'grace@example.org' ? 'u2' : 'u1',
        email: email,
      ),
    );
    when(
      () => authRepo.resolveMfa(
        email: any(named: 'email'),
        mfaCode: any(named: 'mfaCode'),
      ),
    ).thenAnswer(
      (_) async => AuthLoginResult.authenticated(
        user: AuthUser(
          userAccountId: email == 'grace@example.org' ? 'u2' : 'u1',
          email: email,
        ),
      ),
    );
  }

  void stubSession({
    String accountId = 'u1',
    String email = 'ada@example.org',
    String personId = 'p1',
    String preferredName = 'Ada Lovelace',
  }) {
    when(() => memberRepo.loadMemberSession()).thenAnswer(
      (_) async => MemberSessionResult.resolved(
        account: AuthUser(userAccountId: accountId, email: email),
        person: PersonDto(
          id: personId,
          preferredName: preferredName,
          status: 'Active',
          hasLinkedIdentityAccount: true,
          createdOn: DateTime(2020, 1, 1),
        ),
        membership: null,
      ),
    );
  }

  Future<void> signIn(WidgetTester tester, {required String email}) async {
    await tester.enterText(find.widgetWithText(TextFormField, 'Email'), email);
    await tester.enterText(
        find.widgetWithText(TextFormField, 'Password'), 'hunter2');
    await tester.tap(find.byType(FilledButton));
    await pumpFrames(tester);
    expect(find.byType(MfaPage), findsOneWidget);
    await tester.enterText(find.byType(TextFormField), '123456');
    await tester.tap(find.byType(FilledButton));
    await pumpFrames(tester);
  }

  MemberNotificationSummaryDto notif(String id) => MemberNotificationSummaryDto(
        id: id,
        typeCode: 'community-event',
        channel: 'InApp',
        status: 'Sent',
        title: 'Study circle $id',
        body: 'Bring a friend.',
        isRead: false,
        createdOn: DateTime(2026, 9, 1),
      );

  void stubNotifications(List<MemberNotificationSummaryDto> items) {
    when(() => notifRepo.myNotifications(limit: 50, offset: 0))
        .thenAnswer((_) async => items);
    when(() => notifRepo.unreadCount()).thenAnswer((_) async => items.length);
  }

  testWidgets(
      'a /notifications deep link survives sign-in and MFA and resolves '
      'against the member surface', (tester) async {
    when(() => authRepo.restoreSession()).thenAnswer((_) async => null);
    stubSignIn(email: 'ada@example.org');
    when(() => authRepo.logout()).thenAnswer((_) async {});
    stubSession();
    stubNotifications([notif('n-a')]);

    final authBloc = AuthBloc(authRepo, coordinator);
    addTearDown(authBloc.close);
    final router = buildRouter(authBloc);

    authBloc.add(const AuthEvent.appStarted());
    await tester.pumpWidget(harness(authBloc, router));
    await pumpFrames(tester);
    expect(find.byType(LoginPage), findsOneWidget);

    router.push('/notifications');
    await pumpFrames(tester);
    expect(find.byType(LoginPage), findsOneWidget);
    expect(find.byType(NotificationCenterPage), findsNothing);

    await signIn(tester, email: 'ada@example.org');

    expect(find.byType(NotificationCenterPage), findsOneWidget);
    expect(find.text('Study circle n-a'), findsOneWidget);
    verify(() => notifRepo.myNotifications(limit: 50, offset: 0)).called(1);

    await tester.pumpWidget(const SizedBox());
    await tester.pump(const Duration(milliseconds: 1));
  });

  testWidgets('the Home bell opens the Notification Center and issues a fetch',
      (tester) async {
    when(() => authRepo.restoreSession()).thenAnswer((_) async => null);
    stubSignIn(email: 'ada@example.org');
    when(() => authRepo.logout()).thenAnswer((_) async {});
    stubSession();
    stubNotifications([notif('n-a')]);

    final authBloc = AuthBloc(authRepo, coordinator);
    addTearDown(authBloc.close);
    final router = buildRouter(authBloc);

    authBloc.add(const AuthEvent.appStarted());
    await tester.pumpWidget(harness(authBloc, router));
    await pumpFrames(tester);

    await signIn(tester, email: 'ada@example.org');
    expect(find.byType(HomePage), findsOneWidget);

    await tester.tap(find.byKey(const Key('open-notifications')));
    await pumpFrames(tester);

    expect(find.byType(NotificationCenterPage), findsOneWidget);
    expect(find.text('Study circle n-a'), findsOneWidget);
    verify(() => notifRepo.myNotifications(limit: 50, offset: 0)).called(1);

    // back returns to home
    await tester.tap(find.byType(BackButton));
    await pumpFrames(tester);
    expect(find.byType(HomePage), findsOneWidget);
    expect(find.byType(NotificationCenterPage), findsNothing);

    await tester.pumpWidget(const SizedBox());
    await tester.pump(const Duration(milliseconds: 1));
  });

  testWidgets(
      'back navigation returns to home and a second visit issues a '
      'fresh request', (tester) async {
    when(() => authRepo.restoreSession()).thenAnswer((_) async => null);
    stubSignIn(email: 'ada@example.org');
    when(() => authRepo.logout()).thenAnswer((_) async {});
    stubSession();
    stubNotifications([notif('n-a')]);

    final authBloc = AuthBloc(authRepo, coordinator);
    addTearDown(authBloc.close);
    final router = buildRouter(authBloc);

    authBloc.add(const AuthEvent.appStarted());
    await tester.pumpWidget(harness(authBloc, router));
    await pumpFrames(tester);

    await signIn(tester, email: 'ada@example.org');
    expect(find.byType(HomePage), findsOneWidget);

    await tester.tap(find.byKey(const Key('open-notifications')));
    await pumpFrames(tester);
    expect(find.text('Study circle n-a'), findsOneWidget);

    await tester.tap(find.byType(BackButton));
    await pumpFrames(tester);
    expect(find.byType(HomePage), findsOneWidget);

    await tester.tap(find.byKey(const Key('open-notifications')));
    await pumpFrames(tester);
    expect(find.text('Study circle n-a'), findsOneWidget);
    // a second visit performs a fresh authoritative request rather than
    // replaying captured content
    verify(() => notifRepo.myNotifications(limit: 50, offset: 0)).called(2);

    await tester.pumpWidget(const SizedBox());
    await tester.pump(const Duration(milliseconds: 1));
  });
}
