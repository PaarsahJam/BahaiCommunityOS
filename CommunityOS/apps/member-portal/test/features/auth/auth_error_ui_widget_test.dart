import 'dart:async';

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
    when(() => memberRepo.loadMemberSession()).thenAnswer(
      (_) async => MemberSessionResult.resolved(
        account: const AuthUser(userAccountId: 'u1', email: 'ada@example.org'),
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
  });

  Future<void> pumpFrames(WidgetTester tester, [int count = 16]) async {
    for (var i = 0; i < count; i++) {
      await tester.pump(const Duration(milliseconds: 100));
    }
  }

  GoRouter buildRouter(AuthBloc authBloc, MemberSessionBloc sessionBloc) =>
      AppRouter.build(
        authBloc,
        createMemberSession: () => sessionBloc,
      );

  Future<(AuthBloc, MemberSessionBloc, GoRouter)> pumpApp(
      WidgetTester tester) async {
    final authBloc = AuthBloc(authRepo, coordinator);
    final sessionBloc = MemberSessionBloc(memberRepo);
    addTearDown(authBloc.close);
    final router = buildRouter(authBloc, sessionBloc);

    authBloc.add(const AuthEvent.appStarted());
    await tester.pumpWidget(
      BlocProvider<AuthBloc>.value(
        value: authBloc,
        child: MaterialApp.router(
          theme: ThemeData(useMaterial3: true),
          routerConfig: router,
          localizationsDelegates: AppLocalizations.localizationsDelegates,
          supportedLocales: AppLocalizations.supportedLocales,
        ),
      ),
    );
    await pumpFrames(tester);
    return (authBloc, sessionBloc, router);
  }

  Future<void> enterCredentials(WidgetTester tester) async {
    await tester.enterText(
        find.widgetWithText(TextFormField, 'Email'), 'ada@example.org');
    await tester.enterText(
        find.widgetWithText(TextFormField, 'Password'), 'hunter2');
  }

  testWidgets(
      'an invalid-credentials failure shows the localized banner and '
      'stays on sign-in', (tester) async {
    when(() => authRepo.restoreSession()).thenAnswer((_) async => null);
    when(() => authRepo.login(
            email: any(named: 'email'), password: any(named: 'password')))
        .thenThrow(const UnauthorizedException(
      'Invalid email or password.',
      messageKey: 'login_invalidCredentials',
      statusCode: 401,
    ));

    await pumpApp(tester);
    expect(find.byType(LoginPage), findsOneWidget);

    await enterCredentials(tester);
    await tester.tap(find.byType(FilledButton));
    await pumpFrames(tester);

    expect(find.text('Invalid email or password.'), findsOneWidget);
    expect(find.byType(LoginPage), findsOneWidget);
    expect(find.byType(MfaPage), findsNothing);

    await tester.pumpWidget(const SizedBox());
    await tester.pump(const Duration(milliseconds: 1));
  });

  testWidgets(
      'an invalid MFA code shows the localized banner and stays on '
      'the MFA page', (tester) async {
    when(() => authRepo.restoreSession()).thenAnswer((_) async => null);
    when(() => authRepo.login(
            email: any(named: 'email'), password: any(named: 'password')))
        .thenAnswer((_) async => const AuthLoginResult.requiresMfa(
              accountId: 'u1',
              email: 'ada@example.org',
            ));
    when(() => authRepo.resolveMfa(
        email: any(named: 'email'),
        mfaCode: any(named: 'mfaCode'))).thenThrow(const UnauthorizedException(
      'bad code',
      messageKey: 'mfa_invalidCode',
      statusCode: 400,
    ));

    await pumpApp(tester);
    await enterCredentials(tester);
    await tester.tap(find.byType(FilledButton));
    await pumpFrames(tester);
    expect(find.byType(MfaPage), findsOneWidget);

    await tester.enterText(find.byType(TextFormField), '111111');
    await tester.tap(find.byType(FilledButton));
    await pumpFrames(tester);

    expect(find.text('The verification code is invalid. Please try again.'),
        findsOneWidget);
    expect(find.byType(MfaPage), findsOneWidget);
    expect(find.byType(HomePage), findsNothing);

    await tester.pumpWidget(const SizedBox());
    await tester.pump(const Duration(milliseconds: 1));
  });

  testWidgets('"Back to sign in" abandons MFA and returns to the login page',
      (tester) async {
    when(() => authRepo.restoreSession()).thenAnswer((_) async => null);
    when(() => authRepo.login(
            email: any(named: 'email'), password: any(named: 'password')))
        .thenAnswer((_) async => const AuthLoginResult.requiresMfa(
              accountId: 'u1',
              email: 'ada@example.org',
            ));

    await pumpApp(tester);
    await enterCredentials(tester);
    await tester.tap(find.byType(FilledButton));
    await pumpFrames(tester);
    expect(find.byType(MfaPage), findsOneWidget);

    await tester.tap(find.text('Back to sign in'));
    await pumpFrames(tester);

    expect(find.byType(LoginPage), findsOneWidget);
    expect(find.byType(MfaPage), findsNothing);

    await tester.pumpWidget(const SizedBox());
    await tester.pump(const Duration(milliseconds: 1));
  });

  testWidgets(
      'the MFA code field enforces exactly six digits before any '
      'request', (tester) async {
    when(() => authRepo.restoreSession()).thenAnswer((_) async => null);
    when(() => authRepo.login(
            email: any(named: 'email'), password: any(named: 'password')))
        .thenAnswer((_) async => const AuthLoginResult.requiresMfa(
              accountId: 'u1',
              email: 'ada@example.org',
            ));

    await pumpApp(tester);
    await enterCredentials(tester);
    await tester.tap(find.byType(FilledButton));
    await pumpFrames(tester);

    await tester.enterText(find.byType(TextFormField), '12345');
    await tester.tap(find.byType(FilledButton));
    await pumpFrames(tester);

    expect(find.text('Enter exactly 6 digits.'), findsOneWidget);
    verifyNever(() => authRepo.resolveMfa(
        email: any(named: 'email'), mfaCode: any(named: 'mfaCode')));

    await tester.pumpWidget(const SizedBox());
    await tester.pump(const Duration(milliseconds: 1));
  });

  testWidgets(
      'in-flight sign-in announces "Signing in…" to assistive '
      'technology', (tester) async {
    final handle = tester.ensureSemantics();
    when(() => authRepo.restoreSession()).thenAnswer((_) async => null);
    final gate = Completer<AuthLoginResult>();
    when(() => authRepo.login(
        email: any(named: 'email'),
        password: any(named: 'password'))).thenAnswer((_) => gate.future);

    await pumpApp(tester);
    await enterCredentials(tester);
    await tester.tap(find.byType(FilledButton));
    await tester.pump(const Duration(milliseconds: 50));

    expect(find.bySemanticsLabel('Signing in…'), findsOneWidget);

    gate.complete(const AuthLoginResult.requiresMfa(
      accountId: 'u1',
      email: 'ada@example.org',
    ));
    await pumpFrames(tester);
    expect(find.byType(MfaPage), findsOneWidget);

    handle.dispose();
    await tester.pumpWidget(const SizedBox());
    await tester.pump(const Duration(milliseconds: 1));
  });

  testWidgets('in-flight MFA verification announces "Verifying…"',
      (tester) async {
    final handle = tester.ensureSemantics();
    when(() => authRepo.restoreSession()).thenAnswer((_) async => null);
    when(() => authRepo.login(
            email: any(named: 'email'), password: any(named: 'password')))
        .thenAnswer((_) async => const AuthLoginResult.requiresMfa(
              accountId: 'u1',
              email: 'ada@example.org',
            ));
    final gate = Completer<AuthLoginResult>();
    when(() => authRepo.resolveMfa(
        email: any(named: 'email'),
        mfaCode: any(named: 'mfaCode'))).thenAnswer((_) => gate.future);

    await pumpApp(tester);
    await enterCredentials(tester);
    await tester.tap(find.byType(FilledButton));
    await pumpFrames(tester);
    expect(find.byType(MfaPage), findsOneWidget);

    await tester.enterText(find.byType(TextFormField), '123456');
    await tester.tap(find.byType(FilledButton));
    await tester.pump(const Duration(milliseconds: 50));

    expect(find.bySemanticsLabel('Verifying…'), findsOneWidget);

    gate.complete(const AuthLoginResult.authenticated(
      user: AuthUser(userAccountId: 'u1', email: 'ada@example.org'),
    ));
    when(() => authRepo.logout()).thenAnswer((_) async {});
    await pumpFrames(tester);
    expect(find.byType(HomePage), findsOneWidget);

    handle.dispose();
    await tester.pumpWidget(const SizedBox());
    await tester.pump(const Duration(milliseconds: 1));
  });
}
