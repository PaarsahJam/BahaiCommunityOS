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

SessionDto _session({
  String id = 's1',
  String deviceId = 'd1',
  bool isActive = true,
}) =>
    SessionDto(
      id: id,
      deviceId: deviceId,
      createdOn: DateTime(2026, 9, 1, 9),
      expiresOn: DateTime(2026, 9, 8, 9),
      lastUsedOn: DateTime(2026, 9, 7, 12),
      isActive: isActive,
    );

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

  GoRouter buildRouter(AuthBloc authBloc) => AppRouter.build(
        authBloc,
        createMemberSession: () => MemberSessionBloc(memberRepo),
        createAccount: () => AccountBloc(memberRepo),
        createSecurity: () => SecurityBloc(memberRepo),
      );

  void stubSignIn({required String email}) {
    final accountId = email == 'grace@example.org' ? 'u2' : 'u1';
    when(
      () => authRepo.login(
        email: any(named: 'email'),
        password: any(named: 'password'),
      ),
    ).thenAnswer(
      (_) async => AuthLoginResult.requiresMfa(
        accountId: accountId,
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
        user: AuthUser(userAccountId: accountId, email: email),
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
      'account B can never see account A sessions across a switch because '
      'every visit re-fetches the caller-scoped session list', (tester) async {
    when(() => authRepo.restoreSession()).thenAnswer((_) async => null);
    stubSignIn(email: 'ada@example.org');
    when(() => authRepo.logout()).thenAnswer((_) async {});
    stubSignIn(email: 'grace@example.org');

    var currentEmail = '';
    when(() => memberRepo.loadMemberSession()).thenAnswer((_) async {
      final accountId = currentEmail == 'grace@example.org' ? 'u2' : 'u1';
      return MemberSessionResult.resolved(
        account: AuthUser(userAccountId: accountId, email: currentEmail),
        person: PersonDto(
          id: accountId == 'u2' ? 'p2' : 'p1',
          preferredName: currentEmail == 'grace@example.org'
              ? 'Grace Hopper'
              : 'Ada Lovelace',
          status: 'Active',
          hasLinkedIdentityAccount: true,
          createdOn: DateTime(2020, 1, 1),
        ),
        membership: null,
      );
    });
    when(() => memberRepo.loadAccount()).thenAnswer((_) async {
      final accountId = currentEmail == 'grace@example.org' ? 'u2' : 'u1';
      return UserAccountDto(
        id: accountId,
        email: currentEmail,
        status: 'Active',
        createdOn: DateTime(2020, 1, 1),
      );
    });
    when(() => memberRepo.securityEvents())
        .thenAnswer((_) async => <SecurityEventDto>[]);
    var sessionsReads = 0;
    when(() => memberRepo.sessions()).thenAnswer((_) async {
      sessionsReads++;
      if (currentEmail == 'grace@example.org') {
        return [_session(id: 'sb1', deviceId: 'db1')];
      }
      return [_session(id: 'sa1', deviceId: 'da1')];
    });

    final authBloc = AuthBloc(authRepo, coordinator);
    addTearDown(authBloc.close);
    final router = buildRouter(authBloc);

    authBloc.add(const AuthEvent.appStarted());
    tester.view.physicalSize = const Size(800, 2400);
    tester.view.devicePixelRatio = 1.0;
    addTearDown(tester.view.reset);
    await tester.pumpWidget(harness(authBloc, router));
    await pumpFrames(tester);
    expect(find.byType(LoginPage), findsOneWidget);

    // Account A: only A's session is revocable.
    currentEmail = 'ada@example.org';
    await signIn(tester, email: 'ada@example.org');
    expect(find.byType(HomePage), findsOneWidget);
    await tester.tap(find.byIcon(Icons.manage_accounts_outlined));
    await pumpFrames(tester);
    expect(find.byType(AccountPage), findsOneWidget);
    expect(find.byKey(const Key('session-revoke-sa1')), findsOneWidget);
    expect(find.byKey(const Key('session-revoke-sb1')), findsNothing);
    expect(sessionsReads, 1);

    // Log out of account A and sign in as account B.
    await tester.tap(find.byType(BackButton));
    await pumpFrames(tester);
    await tester.tap(find.byIcon(Icons.logout));
    await pumpFrames(tester);
    expect(find.byType(LoginPage), findsOneWidget);

    currentEmail = 'grace@example.org';
    await signIn(tester, email: 'grace@example.org');
    expect(find.byType(HomePage), findsOneWidget);
    await tester.tap(find.byIcon(Icons.manage_accounts_outlined));
    await pumpFrames(tester);

    expect(find.byType(AccountPage), findsOneWidget);
    expect(find.byKey(const Key('session-revoke-sb1')), findsOneWidget);
    // Account A's session is never replayed into B's surface.
    expect(find.byKey(const Key('session-revoke-sa1')), findsNothing);
    expect(sessionsReads, 2);

    await tester.pumpWidget(const SizedBox());
    await tester.pump(const Duration(milliseconds: 1));
  });
}
