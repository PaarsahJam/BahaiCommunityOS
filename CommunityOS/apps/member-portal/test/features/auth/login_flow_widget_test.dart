import 'package:flutter/material.dart';
import 'package:flutter_bloc/flutter_bloc.dart';
import 'package:flutter_test/flutter_test.dart';
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
import 'package:member_portal/features/member/application/member_bloc.dart';
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
  late MemberBloc memberBloc;

  setUp(() {
    authRepo = _MockAuthRepository();
    coordinator = _MockCoordinator();
    when(() => coordinator.onSessionExpired)
        .thenAnswer((_) => const Stream.empty());
    memberRepo = _MockMemberRepository();
    when(() => memberRepo.loadMemberHome()).thenAnswer(
      (_) async => MemberHomeResolved(
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

  Widget harness(AuthBloc authBloc) {
    return MultiBlocProvider(
      providers: [
        BlocProvider<AuthBloc>.value(value: authBloc),
        BlocProvider<MemberBloc>.value(value: memberBloc),
      ],
      child: MaterialApp.router(
        theme: ThemeData(useMaterial3: true),
        routerConfig: AppRouter.build(authBloc),
        localizationsDelegates: AppLocalizations.localizationsDelegates,
        supportedLocales: AppLocalizations.supportedLocales,
      ),
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
      (_) async =>
          const AuthLoginResult.requiresMfa(accountId: 'u1', email: 'ada@example.org'),
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
    memberBloc = MemberBloc(memberRepo);
    addTearDown(authBloc.close);
    addTearDown(memberBloc.close);

    authBloc.add(const AuthEvent.appStarted());
    await tester.pumpWidget(harness(authBloc));
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
    memberBloc = MemberBloc(memberRepo);
    addTearDown(authBloc.close);
    addTearDown(memberBloc.close);

    authBloc.add(const AuthEvent.appStarted());
    await tester.pumpWidget(harness(authBloc));
    await pumpFrames(tester);

    expect(find.byType(HomePage), findsOneWidget);
    expect(find.byType(SplashPage), findsNothing);
    expect(find.byType(LoginPage), findsNothing);
    expect(find.text('Ada Lovelace'), findsOneWidget);
  });
}