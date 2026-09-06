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
import 'package:member_portal/features/member/presentation/membership_page.dart';
import 'package:member_portal/l10n/generated/app_localizations.dart';
import 'package:mocktail/mocktail.dart';

class _MockAuthRepository extends Mock implements AuthRepository {}

class _MockCoordinator extends Mock implements RefreshCoordinator {}

class _MockMemberRepository extends Mock implements MemberRepository {}

void main() {
  late _MockAuthRepository authRepo;
  late _MockCoordinator coordinator;
  late _MockMemberRepository memberRepo;

  setUp(() {
    authRepo = _MockAuthRepository();
    coordinator = _MockCoordinator();
    when(() => coordinator.onSessionExpired)
        .thenAnswer((_) => const Stream.empty());
    memberRepo = _MockMemberRepository();
    when(() => memberRepo.getMembership(any())).thenAnswer((_) async => null);
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

  testWidgets(
      'a /membership deep link survives sign-in and MFA and resolves '
      'against the session person', (tester) async {
    when(() => authRepo.restoreSession()).thenAnswer((_) async => null);
    stubSignIn(email: 'ada@example.org');
    when(() => authRepo.logout()).thenAnswer((_) async {});
    stubSession();

    final authBloc = AuthBloc(authRepo, coordinator);
    addTearDown(authBloc.close);
    final router = buildRouter(authBloc);

    authBloc.add(const AuthEvent.appStarted());
    await tester.pumpWidget(harness(authBloc, router));
    await pumpFrames(tester);
    expect(find.byType(LoginPage), findsOneWidget);

    // Attempting the protected membership page while unauthenticated records
    // the destination and stays on sign-in.
    router.push('/membership');
    await pumpFrames(tester);
    expect(find.byType(LoginPage), findsOneWidget);
    expect(find.byType(MembershipPage), findsNothing);

    await signIn(tester, email: 'ada@example.org');

    expect(find.byType(MembershipPage), findsOneWidget);
    verify(() => memberRepo.getMembership('p1')).called(1);

    await tester.pumpWidget(const SizedBox());
    await tester.pump(const Duration(milliseconds: 1));
  });

  testWidgets(
      'a stale pending membership route cannot be replayed by a fresh session '
      'after logout', (tester) async {
    when(() => authRepo.restoreSession()).thenAnswer((_) async => null);
    stubSignIn(email: 'ada@example.org');
    when(() => authRepo.logout()).thenAnswer((_) async {});
    stubSession();

    final authBloc = AuthBloc(authRepo, coordinator);
    addTearDown(authBloc.close);
    final router = buildRouter(authBloc);

    authBloc.add(const AuthEvent.appStarted());
    await tester.pumpWidget(harness(authBloc, router));
    await pumpFrames(tester);
    expect(find.byType(LoginPage), findsOneWidget);

    await signIn(tester, email: 'ada@example.org');
    expect(find.byType(HomePage), findsOneWidget);

    // Logout clears any pending destination and ends the session.
    await tester.tap(find.byIcon(Icons.logout));
    await pumpFrames(tester);
    expect(find.byType(LoginPage), findsOneWidget);
    expect(find.byType(HomePage), findsNothing);

    // The same account signs back in without recording any new destination.
    // Nothing from the previous session may be replayed, including a stale
    // membership route.
    await signIn(tester, email: 'ada@example.org');

    expect(find.byType(HomePage), findsOneWidget);
    expect(find.byType(MembershipPage), findsNothing);
    verifyNever(() => memberRepo.getMembership('p1'));

    await tester.pumpWidget(const SizedBox());
    await tester.pump(const Duration(milliseconds: 1));
  });

  testWidgets('account B can never see account A membership across a switch',
      (tester) async {
    when(() => authRepo.restoreSession()).thenAnswer((_) async => null);
    stubSignIn(email: 'ada@example.org');
    when(() => authRepo.logout()).thenAnswer((_) async {});
    stubSignIn(email: 'grace@example.org');
    stubSession(); // first session: account A / person p1

    final authBloc = AuthBloc(authRepo, coordinator);
    addTearDown(authBloc.close);
    final router = buildRouter(authBloc);

    authBloc.add(const AuthEvent.appStarted());
    await tester.pumpWidget(harness(authBloc, router));
    await pumpFrames(tester);

    await signIn(tester, email: 'ada@example.org');
    expect(find.byType(HomePage), findsOneWidget);

    await tester.tap(find.text('Membership'));
    await pumpFrames(tester);
    expect(find.byType(MembershipPage), findsOneWidget);
    verify(() => memberRepo.getMembership('p1')).called(1);

    await tester.tap(find.byType(BackButton));
    await pumpFrames(tester);
    expect(find.byType(HomePage), findsOneWidget);

    // Log out of account A.
    await tester.tap(find.byIcon(Icons.logout));
    await pumpFrames(tester);
    expect(find.byType(LoginPage), findsOneWidget);

    // Second session context is account B / person p2.
    stubSession(
      accountId: 'u2',
      email: 'grace@example.org',
      personId: 'p2',
      preferredName: 'Grace Hopper',
    );

    router.push('/membership');
    await pumpFrames(tester);
    expect(find.byType(LoginPage), findsOneWidget);

    await signIn(tester, email: 'grace@example.org');

    expect(find.byType(MembershipPage), findsOneWidget);
    verify(() => memberRepo.getMembership('p2')).called(1);
    verifyNever(() => memberRepo.getMembership('p1'));
    expect(find.text('Grace Hopper'), findsNothing);

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

    final authBloc = AuthBloc(authRepo, coordinator);
    addTearDown(authBloc.close);
    final router = buildRouter(authBloc);

    authBloc.add(const AuthEvent.appStarted());
    await tester.pumpWidget(harness(authBloc, router));
    await pumpFrames(tester);

    await signIn(tester, email: 'ada@example.org');
    expect(find.byType(HomePage), findsOneWidget);

    await tester.tap(find.text('Membership'));
    await pumpFrames(tester);
    expect(find.byType(MembershipPage), findsOneWidget);
    verify(() => memberRepo.getMembership('p1')).called(1);

    await tester.tap(find.byType(BackButton));
    await pumpFrames(tester);
    expect(find.byType(HomePage), findsOneWidget);
    expect(find.byType(MembershipPage), findsNothing);

    await tester.tap(find.text('Membership'));
    await pumpFrames(tester);
    expect(find.byType(MembershipPage), findsOneWidget);
    // mocktail counts *unverified* calls only, so this asserts precisely that
    // the second visit performed a fresh authoritative request.
    verify(() => memberRepo.getMembership('p1')).called(1);

    await tester.pumpWidget(const SizedBox());
    await tester.pump(const Duration(milliseconds: 1));
  });
}
