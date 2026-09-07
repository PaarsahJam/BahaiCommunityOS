import 'package:flutter/material.dart';
import 'package:flutter_bloc/flutter_bloc.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:go_router/go_router.dart';
import 'package:member_portal/core/network/refresh_coordinator.dart';
import 'package:member_portal/core/router/app_router.dart';
import 'package:member_portal/features/account/application/account_bloc.dart';
import 'package:member_portal/features/account/application/security_bloc.dart';
import 'package:member_portal/features/account/presentation/account_page.dart';
import 'package:member_portal/features/auth/application/auth_bloc.dart';
import 'package:member_portal/features/auth/application/auth_event.dart';
import 'package:member_portal/features/auth/data/auth_dtos.dart';
import 'package:member_portal/features/auth/domain/auth_models.dart';
import 'package:member_portal/features/auth/domain/auth_repository.dart';
import 'package:member_portal/features/auth/presentation/login_page.dart';
import 'package:member_portal/features/auth/presentation/mfa_page.dart';
import 'package:member_portal/features/member/application/member_session_bloc.dart';
import 'package:member_portal/features/member/data/member_dtos.dart';
import 'package:member_portal/features/member/domain/member_models.dart';
import 'package:member_portal/features/member/domain/member_repository.dart';
import 'package:member_portal/features/member/presentation/home_page.dart';
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

  Future<void> pumpRouterHarness(
    WidgetTester tester,
    AuthBloc authBloc,
    GoRouter router,
  ) async {
    // Account content spans several cards; a tall viewport keeps the password
    // fields inside the lazily-built ListView so they are findable/tappable.
    tester.view.physicalSize = const Size(800, 2400);
    tester.view.devicePixelRatio = 1.0;
    addTearDown(tester.view.reset);
    await tester.pumpWidget(harness(authBloc, router));
    await pumpFrames(tester);
  }

  GoRouter buildRouter(AuthBloc authBloc) => AppRouter.build(
        authBloc,
        createMemberSession: () => MemberSessionBloc(memberRepo),
        createAccount: () => AccountBloc(memberRepo),
        createSecurity: () => SecurityBloc(memberRepo),
      );

  void stubAccount() {
    when(() => memberRepo.loadAccount()).thenAnswer(
      (_) async => UserAccountDto(
        id: 'u1',
        email: 'ada@example.org',
        status: 'Active',
        createdOn: DateTime(2020, 1, 1),
      ),
    );
    when(() => memberRepo.securityEvents()).thenAnswer((_) async => [
          SecurityEventDto(
            id: 'e1',
            eventType: 'Login.Succeeded',
            occurredOn: DateTime(2026, 9, 7, 12),
          ),
        ]);
    // The Identity & Security section loads the read-only session list when
    // the account page mounts.
    when(() => memberRepo.sessions()).thenAnswer((_) async => <SessionDto>[]);
  }

  void stubSignIn({required String email}) {
    when(
      () => authRepo.login(
        email: any(named: 'email'),
        password: any(named: 'password'),
      ),
    ).thenAnswer(
      (_) async => AuthLoginResult.requiresMfa(
        accountId: 'u1',
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
        user: AuthUser(userAccountId: 'u1', email: email),
      ),
    );
  }

  void stubSession({String email = 'ada@example.org'}) {
    when(() => memberRepo.loadMemberSession()).thenAnswer(
      (_) async => MemberSessionResult.resolved(
        account: AuthUser(userAccountId: 'u1', email: email),
        person: PersonDto(
          id: 'p1',
          preferredName: 'Ada Lovelace',
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

  Future<void> fillPasswordForm(WidgetTester tester) async {
    await tester.enterText(
        find.byKey(const Key('current-password')), 'old-pass');
    await tester.enterText(find.byKey(const Key('new-password')), 'new-pass');
    await tester.enterText(
        find.byKey(const Key('confirm-password')), 'new-pass');
  }

  testWidgets(
      'a /account deep link survives sign-in and MFA and loads the account',
      (tester) async {
    when(() => authRepo.restoreSession()).thenAnswer((_) async => null);
    stubSignIn(email: 'ada@example.org');
    when(() => authRepo.logout()).thenAnswer((_) async {});
    stubSession();
    stubAccount();

    final authBloc = AuthBloc(authRepo, coordinator);
    addTearDown(authBloc.close);
    final router = buildRouter(authBloc);

    authBloc.add(const AuthEvent.appStarted());
    await pumpRouterHarness(tester, authBloc, router);
    expect(find.byType(LoginPage), findsOneWidget);

    router.push('/account');
    await pumpFrames(tester);
    expect(find.byType(LoginPage), findsOneWidget);
    expect(find.byType(AccountPage), findsNothing);

    await signIn(tester, email: 'ada@example.org');

    expect(find.byType(AccountPage), findsOneWidget);
    expect(find.text('ada@example.org'), findsOneWidget);
    verify(() => memberRepo.loadAccount()).called(1);

    await tester.pumpWidget(const SizedBox());
    await tester.pump(const Duration(milliseconds: 1));
  });

  testWidgets(
      'the home toolbar account entry opens the account page and back returns '
      'home', (tester) async {
    when(() => authRepo.restoreSession()).thenAnswer((_) async => null);
    stubSignIn(email: 'ada@example.org');
    when(() => authRepo.logout()).thenAnswer((_) async {});
    stubSession();
    stubAccount();

    final authBloc = AuthBloc(authRepo, coordinator);
    addTearDown(authBloc.close);
    final router = buildRouter(authBloc);

    authBloc.add(const AuthEvent.appStarted());
    await pumpRouterHarness(tester, authBloc, router);

    await signIn(tester, email: 'ada@example.org');
    expect(find.byType(HomePage), findsOneWidget);
    expect(find.byType(AccountPage), findsNothing);

    await tester.tap(find.byIcon(Icons.manage_accounts_outlined));
    await pumpFrames(tester);

    expect(find.byType(AccountPage), findsOneWidget);
    verify(() => memberRepo.loadAccount()).called(1);

    await tester.tap(find.byType(BackButton));
    await pumpFrames(tester);

    expect(find.byType(HomePage), findsOneWidget);
    expect(find.byType(AccountPage), findsNothing);

    await tester.pumpWidget(const SizedBox());
    await tester.pump(const Duration(milliseconds: 1));
  });

  testWidgets(
      'a successful password change deliberately re-authenticates and lands '
      'on sign-in', (tester) async {
    when(() => authRepo.restoreSession()).thenAnswer((_) async => null);
    stubSignIn(email: 'ada@example.org');
    when(() => authRepo.logout()).thenAnswer((_) async {});
    stubSession();
    stubAccount();
    when(() => authRepo.clearLocalAuth()).thenAnswer((_) async {});
    when(() => memberRepo.changePassword(
        currentPassword: any(named: 'currentPassword'),
        newPassword: any(named: 'newPassword'))).thenAnswer((_) async {});

    final authBloc = AuthBloc(authRepo, coordinator);
    addTearDown(authBloc.close);
    final router = buildRouter(authBloc);

    authBloc.add(const AuthEvent.appStarted());
    await pumpRouterHarness(tester, authBloc, router);

    await signIn(tester, email: 'ada@example.org');
    expect(find.byType(HomePage), findsOneWidget);

    router.push('/account');
    await pumpFrames(tester);
    expect(find.byType(AccountPage), findsOneWidget);

    await fillPasswordForm(tester);
    await tester.tap(find.widgetWithText(FilledButton, 'Change password'));
    await pumpFrames(tester);

    verify(() => authRepo.clearLocalAuth()).called(1);
    expect(find.byType(LoginPage), findsOneWidget);
    expect(find.byType(AccountPage), findsNothing);
    expect(find.byType(HomePage), findsNothing);

    await tester.pumpWidget(const SizedBox());
    await tester.pump(const Duration(milliseconds: 1));
  });

  testWidgets(
      'an incorrect current password stays on the account page and never '
      'tears the session down', (tester) async {
    when(() => authRepo.restoreSession()).thenAnswer((_) async => null);
    stubSignIn(email: 'ada@example.org');
    when(() => authRepo.logout()).thenAnswer((_) async {});
    stubSession();
    stubAccount();
    when(() => memberRepo.changePassword(
            currentPassword: any(named: 'currentPassword'),
            newPassword: any(named: 'newPassword')))
        .thenThrow(const UnauthorizedException(
      'The current password is incorrect.',
      messageKey: 'accountCurrentPasswordIncorrect',
      statusCode: 401,
    ));

    final authBloc = AuthBloc(authRepo, coordinator);
    addTearDown(authBloc.close);
    final router = buildRouter(authBloc);

    authBloc.add(const AuthEvent.appStarted());
    await pumpRouterHarness(tester, authBloc, router);

    await signIn(tester, email: 'ada@example.org');
    router.push('/account');
    await pumpFrames(tester);
    expect(find.byType(AccountPage), findsOneWidget);

    await fillPasswordForm(tester);
    await tester.tap(find.widgetWithText(FilledButton, 'Change password'));
    await pumpFrames(tester);

    expect(find.byType(AccountPage), findsOneWidget);
    expect(find.byType(LoginPage), findsNothing);
    expect(find.text('Your current password is incorrect.'), findsOneWidget);
    verifyNever(() => authRepo.clearLocalAuth());

    await tester.pumpWidget(const SizedBox());
    await tester.pump(const Duration(milliseconds: 1));
  });

  testWidgets(
      'a second visit to the account page issues a fresh authoritative '
      'request', (tester) async {
    when(() => authRepo.restoreSession()).thenAnswer((_) async => null);
    stubSignIn(email: 'ada@example.org');
    when(() => authRepo.logout()).thenAnswer((_) async {});
    stubSession();
    stubAccount();

    final authBloc = AuthBloc(authRepo, coordinator);
    addTearDown(authBloc.close);
    final router = buildRouter(authBloc);

    authBloc.add(const AuthEvent.appStarted());
    await pumpRouterHarness(tester, authBloc, router);

    await signIn(tester, email: 'ada@example.org');
    await tester.tap(find.byIcon(Icons.manage_accounts_outlined));
    await pumpFrames(tester);
    expect(find.byType(AccountPage), findsOneWidget);
    verify(() => memberRepo.loadAccount()).called(1);

    await tester.tap(find.byType(BackButton));
    await pumpFrames(tester);
    expect(find.byType(HomePage), findsOneWidget);

    await tester.tap(find.byIcon(Icons.manage_accounts_outlined));
    await pumpFrames(tester);
    expect(find.byType(AccountPage), findsOneWidget);
    verify(() => memberRepo.loadAccount()).called(1);

    await tester.pumpWidget(const SizedBox());
    await tester.pump(const Duration(milliseconds: 1));
  });
}
