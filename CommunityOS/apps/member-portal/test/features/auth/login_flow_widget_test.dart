import 'package:flutter/material.dart';
import 'package:flutter_bloc/flutter_bloc.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:go_router/go_router.dart';
import 'package:member_portal/core/network/refresh_coordinator.dart';
import 'package:member_portal/core/router/app_router.dart';
import 'package:member_portal/core/storage/token_storage.dart';
import 'package:member_portal/core/ui/splash_page.dart';
import 'package:member_portal/features/auth/application/auth_bloc.dart';
import 'package:member_portal/features/auth/application/auth_event.dart';
import 'package:member_portal/features/auth/domain/auth_models.dart';
import 'package:member_portal/features/auth/domain/auth_repository.dart';
import 'package:member_portal/features/auth/presentation/login_page.dart';
import 'package:member_portal/features/auth/presentation/mfa_page.dart';
import 'package:member_portal/features/member/application/member_session_bloc.dart';
import 'package:member_portal/features/member/application/profile_bloc.dart';
import 'package:member_portal/features/member/data/member_dtos.dart';
import 'package:member_portal/features/member/domain/member_models.dart';
import 'package:member_portal/features/member/domain/member_repository.dart';
import 'package:member_portal/features/member/presentation/home_page.dart';
import 'package:member_portal/features/member/presentation/profile_page.dart';
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
        account: const AuthUser(
          userAccountId: 'u1',
          email: 'ada@example.org',
        ),
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

  GoRouter buildRouter(
    AuthBloc authBloc,
    MemberSessionBloc sessionBloc, {
    ProfileBloc Function()? createProfile,
  }) {
    return AppRouter.build(
      authBloc,
      createMemberSession: () => sessionBloc,
      createProfile: createProfile,
    );
  }

  testWidgets('sign-in flows through MFA to the member home', (tester) async {
    when(() => authRepo.restoreSession()).thenAnswer((_) async => null);
    when(
      () => authRepo.login(
        email: any(named: 'email'),
        password: any(named: 'password'),
      ),
    ).thenAnswer(
      (_) async => const AuthLoginResult.requiresMfa(
        accountId: 'u1',
        email: 'ada@example.org',
      ),
    );
    when(
      () => authRepo.resolveMfa(
        email: any(named: 'email'),
        mfaCode: any(named: 'mfaCode'),
      ),
    ).thenAnswer(
      (_) async => const AuthLoginResult.authenticated(
        user: AuthUser(userAccountId: 'u1', email: 'ada@example.org'),
      ),
    );
    when(() => authRepo.logout()).thenAnswer((_) async {});

    final authBloc = AuthBloc(authRepo, coordinator);
    final sessionBloc = MemberSessionBloc(memberRepo);
    addTearDown(authBloc.close);

    authBloc.add(const AuthEvent.appStarted());
    await tester
        .pumpWidget(harness(authBloc, buildRouter(authBloc, sessionBloc)));
    await pumpFrames(tester);

    expect(find.byType(LoginPage), findsOneWidget);
    expect(find.byType(SplashPage), findsNothing);

    await tester.enterText(
        find.widgetWithText(TextFormField, 'Email'), 'ada@example.org');
    await tester.enterText(
        find.widgetWithText(TextFormField, 'Password'), 'hunter2');
    await tester.tap(find.byType(FilledButton));
    await pumpFrames(tester);

    expect(find.byType(MfaPage), findsOneWidget);

    await tester.enterText(find.byType(TextFormField), '123456');
    await tester.tap(find.byType(FilledButton));
    await pumpFrames(tester);

    expect(find.byType(HomePage), findsOneWidget);
    expect(find.byType(SplashPage), findsNothing);
    expect(find.text('Ada Lovelace'), findsOneWidget);

    await tester.tap(find.byIcon(Icons.logout));
    await pumpFrames(tester);

    expect(find.byType(LoginPage), findsOneWidget);
    expect(find.byType(HomePage), findsNothing);
  });

  testWidgets('a restored session lands on the member home, not the splash',
      (tester) async {
    when(() => authRepo.restoreSession()).thenAnswer(
      (_) async => TokenPair(
        accessToken: 'access-1',
        refreshToken: 'refresh-1',
        expiresAt: DateTime(2030),
      ),
    );
    when(() => authRepo.currentUser()).thenAnswer(
      (_) async => const AuthUser(
        userAccountId: 'u1',
        email: 'ada@example.org',
        status: 'Active',
      ),
    );

    final authBloc = AuthBloc(authRepo, coordinator);
    final sessionBloc = MemberSessionBloc(memberRepo);
    addTearDown(authBloc.close);

    authBloc.add(const AuthEvent.appStarted());
    await tester
        .pumpWidget(harness(authBloc, buildRouter(authBloc, sessionBloc)));
    await pumpFrames(tester);

    expect(find.byType(HomePage), findsOneWidget);
    expect(find.byType(SplashPage), findsNothing);
    expect(find.byType(LoginPage), findsNothing);
    expect(find.text('Ada Lovelace'), findsOneWidget);

    await tester.pumpWidget(const SizedBox());
    await tester.pump(const Duration(milliseconds: 1));
  });

  testWidgets(
      'a protected destination survives sign-in and MFA: deep link lands on '
      'the member profile', (tester) async {
    when(() => authRepo.restoreSession()).thenAnswer((_) async => null);
    when(
      () => authRepo.login(
        email: any(named: 'email'),
        password: any(named: 'password'),
      ),
    ).thenAnswer(
      (_) async => const AuthLoginResult.requiresMfa(
        accountId: 'u1',
        email: 'ada@example.org',
      ),
    );
    when(
      () => authRepo.resolveMfa(
        email: any(named: 'email'),
        mfaCode: any(named: 'mfaCode'),
      ),
    ).thenAnswer(
      (_) async => const AuthLoginResult.authenticated(
        user: AuthUser(userAccountId: 'u1', email: 'ada@example.org'),
      ),
    );
    when(() => authRepo.logout()).thenAnswer((_) async {});
    when(() => memberRepo.personDetail('p1')).thenAnswer(
      (_) async => PersonDetailDto(
        id: 'p1',
        preferredName: 'Ada Lovelace',
        status: 'Active',
        profileVisibility: 'Self',
        contactVisibility: 'Self',
        dateOfBirthVisibility: 'Self',
        createdOn: DateTime(2020, 1, 1),
      ),
    );

    final authBloc = AuthBloc(authRepo, coordinator);
    final sessionBloc = MemberSessionBloc(memberRepo);
    addTearDown(authBloc.close);
    final router = buildRouter(
      authBloc,
      sessionBloc,
      createProfile: () => ProfileBloc(memberRepo),
    );

    authBloc.add(const AuthEvent.appStarted());
    await tester.pumpWidget(harness(authBloc, router));
    await pumpFrames(tester);
    expect(find.byType(LoginPage), findsOneWidget);

    // Attempting the protected member profile while unauthenticated records
    // the destination and stays on sign-in.
    router.push('/profile/p1');
    await pumpFrames(tester);
    expect(find.byType(LoginPage), findsOneWidget);
    expect(find.byType(ProfilePage), findsNothing);

    await tester.enterText(
        find.widgetWithText(TextFormField, 'Email'), 'ada@example.org');
    await tester.enterText(
        find.widgetWithText(TextFormField, 'Password'), 'hunter2');
    await tester.tap(find.byType(FilledButton));
    await pumpFrames(tester);
    expect(find.byType(MfaPage), findsOneWidget);

    await tester.enterText(find.byType(TextFormField), '123456');
    await tester.tap(find.byType(FilledButton));
    await pumpFrames(tester);

    // Sign-in + MFA restored the pending destination: the profile for p1,
    // keyed by the Community PersonId and not the UserAccountId.
    expect(find.byType(ProfilePage), findsOneWidget);
    verify(() => memberRepo.personDetail('p1')).called(1);
    expect(find.text('Ada Lovelace'), findsOneWidget);

    await tester.pumpWidget(const SizedBox());
    await tester.pump(const Duration(milliseconds: 1));
  });

  testWidgets(
      'logout followed by a protected deep link restores the destination for '
      'the next session only', (tester) async {
    when(() => authRepo.restoreSession()).thenAnswer((_) async => null);
    when(
      () => authRepo.login(
        email: any(named: 'email'),
        password: any(named: 'password'),
      ),
    ).thenAnswer((invocation) async {
      final email = invocation.namedArguments[#email] as String;
      if (email == 'grace@example.org') {
        return const AuthLoginResult.requiresMfa(
          accountId: 'u2',
          email: 'grace@example.org',
        );
      }
      return AuthLoginResult.requiresMfa(accountId: 'u1', email: email);
    });
    when(
      () => authRepo.resolveMfa(
        email: any(named: 'email'),
        mfaCode: any(named: 'mfaCode'),
      ),
    ).thenAnswer((invocation) async {
      final email = invocation.namedArguments[#email] as String;
      if (email == 'grace@example.org') {
        return const AuthLoginResult.authenticated(
          user: AuthUser(userAccountId: 'u2', email: 'grace@example.org'),
        );
      }
      return const AuthLoginResult.authenticated(
        user: AuthUser(userAccountId: 'u1', email: 'ada@example.org'),
      );
    });
    when(() => authRepo.logout()).thenAnswer((_) async {});

    // The second session belongs to a different account and must never see
    // the first account's context. mocktail replays the latest stub for every
    // call, so the stub branches on the session ordinal: first (account A) vs
    // later (account B).
    var sessionLoads = 0;
    when(() => memberRepo.loadMemberSession()).thenAnswer(
      (_) async {
        sessionLoads += 1;
        if (sessionLoads == 1) {
          return MemberSessionResult.resolved(
            account: const AuthUser(
              userAccountId: 'u1',
              email: 'ada@example.org',
            ),
            person: PersonDto(
              id: 'p1',
              preferredName: 'Ada Lovelace',
              status: 'Active',
              hasLinkedIdentityAccount: true,
              createdOn: DateTime(2020, 1, 1),
            ),
            membership: null,
          );
        }
        return MemberSessionResult.resolved(
          account: const AuthUser(
            userAccountId: 'u2',
            email: 'grace@example.org',
          ),
          person: PersonDto(
            id: 'p2',
            preferredName: 'Grace Hopper',
            status: 'Active',
            hasLinkedIdentityAccount: true,
            createdOn: DateTime(2020, 1, 1),
          ),
          membership: null,
        );
      },
    );
    when(() => memberRepo.personDetail('p2')).thenAnswer(
      (_) async => PersonDetailDto(
        id: 'p2',
        preferredName: 'Grace Hopper',
        status: 'Active',
        profileVisibility: 'Self',
        contactVisibility: 'Self',
        dateOfBirthVisibility: 'Self',
        createdOn: DateTime(2020, 1, 1),
      ),
    );

    final authBloc = AuthBloc(authRepo, coordinator);
    addTearDown(authBloc.close);
    // Each authenticated session owns a fresh MemberSessionBloc (as in
    // production); the first shell unmounts and closes its own.
    final router = AppRouter.build(
      authBloc,
      createMemberSession: () => MemberSessionBloc(memberRepo),
      createProfile: () => ProfileBloc(memberRepo),
    );

    authBloc.add(const AuthEvent.appStarted());
    await tester.pumpWidget(harness(authBloc, router));
    await pumpFrames(tester);
    expect(find.byType(LoginPage), findsOneWidget);

    // First session: account A signs in through MFA and lands on home.
    await tester.enterText(
        find.widgetWithText(TextFormField, 'Email'), 'ada@example.org');
    await tester.enterText(
        find.widgetWithText(TextFormField, 'Password'), 'hunter2');
    await tester.tap(find.byType(FilledButton));
    await pumpFrames(tester);
    expect(find.byType(MfaPage), findsOneWidget);

    await tester.enterText(find.byType(TextFormField), '123456');
    await tester.tap(find.byType(FilledButton));
    await pumpFrames(tester);
    expect(find.byType(HomePage), findsOneWidget);
    expect(find.text('Ada Lovelace'), findsOneWidget);

    // Logout clears the pending route for account A.
    await tester.tap(find.byIcon(Icons.logout));
    await pumpFrames(tester);
    expect(find.byType(LoginPage), findsOneWidget);
    expect(find.byType(HomePage), findsNothing);

    // A protected destination attempted now belongs to the *next* session.
    router.push('/profile/p2');
    await pumpFrames(tester);
    expect(find.byType(LoginPage), findsOneWidget);
    expect(find.byType(ProfilePage), findsNothing);

    // Second session: account B signs in through MFA and the pending
    // destination is restored against B's context — never account A's.
    await tester.enterText(
        find.widgetWithText(TextFormField, 'Email'), 'grace@example.org');
    await tester.enterText(
        find.widgetWithText(TextFormField, 'Password'), 'hunter2');
    await tester.tap(find.byType(FilledButton));
    await pumpFrames(tester);
    expect(find.byType(MfaPage), findsOneWidget);

    await tester.enterText(find.byType(TextFormField), '123456');
    await tester.tap(find.byType(FilledButton));
    await pumpFrames(tester);

    expect(find.byType(ProfilePage), findsOneWidget);
    verify(() => memberRepo.personDetail('p2')).called(1);
    verifyNever(() => memberRepo.personDetail('p1'));
    expect(find.text('Grace Hopper'), findsOneWidget);
    expect(find.text('Ada Lovelace'), findsNothing);

    await tester.pumpWidget(const SizedBox());
    await tester.pump(const Duration(milliseconds: 1));
  });
}
