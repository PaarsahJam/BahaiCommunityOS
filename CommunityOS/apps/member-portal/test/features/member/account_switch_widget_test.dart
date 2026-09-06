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

MemberSessionResult _accountA() => MemberSessionResult.resolved(
      account: const AuthUser(userAccountId: 'u1', email: 'ada@example.org'),
      person: PersonDto(
        id: 'p1',
        preferredName: 'Ada Lovelace',
        status: 'Active',
        hasLinkedIdentityAccount: true,
        createdOn: DateTime(2020, 1, 1),
      ),
      membership: MembershipDto(
        id: 'm1',
        personId: 'p1',
        status: 'Active',
        effectiveFrom: DateTime(2021, 3, 1),
      ),
    );

MemberSessionResult _accountB() => MemberSessionResult.resolved(
      account: const AuthUser(userAccountId: 'u2', email: 'grace@example.org'),
      person: PersonDto(
        id: 'p2',
        preferredName: 'Grace Hopper',
        status: 'Lapsed',
        hasLinkedIdentityAccount: true,
        createdOn: DateTime(2019, 5, 20),
      ),
      membership: MembershipDto(
        id: 'm2',
        personId: 'p2',
        status: 'Lapsed',
        effectiveFrom: DateTime(2020, 2, 15),
      ),
    );

PersonDetailDto _profileA() => PersonDetailDto(
      id: 'p1',
      preferredName: 'Ada Lovelace',
      dateOfBirth: DateTime(1987, 4, 14),
      status: 'Active',
      identityAccountId: 'u1',
      profileVisibility: 'Self',
      contactVisibility: 'Self',
      dateOfBirthVisibility: 'Self',
      contactMethods: [
        const ContactMethodDto(
          id: 'c1',
          type: 'email',
          value: 'ada@example.org',
          isPreferred: true,
          visibility: 'Self',
        ),
      ],
      createdOn: DateTime(2020, 1, 1),
    );

PersonDetailDto _profileB() => PersonDetailDto(
      id: 'p2',
      preferredName: 'Grace Hopper',
      dateOfBirth: DateTime(1906, 12, 9),
      status: 'Lapsed',
      identityAccountId: 'u2',
      profileVisibility: 'Self',
      contactVisibility: 'Self',
      dateOfBirthVisibility: 'Self',
      contactMethods: [
        const ContactMethodDto(
          id: 'c2',
          type: 'email',
          value: 'grace@example.org',
          isPreferred: true,
          visibility: 'Self',
        ),
      ],
      createdOn: DateTime(2019, 5, 20),
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
    when(() => memberRepo.loadMemberSession())
        .thenAnswer((_) async => _accountA());
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

  GoRouter buildRouter(AuthBloc authBloc) {
    return AppRouter.build(
      authBloc,
      // A fresh session bloc is created and owned by every shell mount, exactly
      // like production. Account B must never inherit Account A's state.
      createMemberSession: () => MemberSessionBloc(memberRepo),
      createProfile: () => ProfileBloc(memberRepo),
    );
  }

  void stubSignIn({
    required String email,
    required String accountId,
  }) {
    when(
      () => authRepo.login(
        email: email,
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
        email: email,
        mfaCode: any(named: 'mfaCode'),
      ),
    ).thenAnswer(
      (_) async => AuthLoginResult.authenticated(
        user: AuthUser(userAccountId: accountId, email: email),
      ),
    );
  }

  Future<void> signIn(
    WidgetTester tester, {
    required String email,
    required String password,
    required String code,
  }) async {
    expect(find.byType(LoginPage), findsOneWidget);
    await tester.enterText(find.widgetWithText(TextFormField, 'Email'), email);
    await tester.enterText(
        find.widgetWithText(TextFormField, 'Password'), password);
    await tester.tap(find.byType(FilledButton));
    await pumpFrames(tester);
    expect(find.byType(MfaPage), findsOneWidget);
    await tester.enterText(find.byType(TextFormField), code);
    await tester.tap(find.byType(FilledButton));
    await pumpFrames(tester);
    expect(find.byType(HomePage), findsOneWidget);
  }

  testWidgets(
      'account switch: Account B only ever sees B data, never stale A data',
      (tester) async {
    when(() => authRepo.restoreSession()).thenAnswer((_) async => null);
    when(() => authRepo.logout()).thenAnswer((_) async {});
    stubSignIn(
      email: 'ada@example.org',
      accountId: 'u1',
    );
    stubSignIn(
      email: 'grace@example.org',
      accountId: 'u2',
    );
    when(() => memberRepo.personDetail('p1'))
        .thenAnswer((_) async => _profileA());
    when(() => memberRepo.personDetail('p2'))
        .thenAnswer((_) async => _profileB());

    final authBloc = AuthBloc(authRepo, coordinator);
    addTearDown(authBloc.close);
    final router = buildRouter(authBloc);

    authBloc.add(const AuthEvent.appStarted());
    await tester.pumpWidget(harness(authBloc, router));
    await pumpFrames(tester);

    // --- Account A signs in and reaches A-only home data. ---
    await signIn(
      tester,
      email: 'ada@example.org',
      password: 'hunter2',
      code: '123456',
    );
    expect(find.text('Ada Lovelace'), findsOneWidget);
    expect(find.text('Grace Hopper'), findsNothing);
    expect(find.text('Member Portal'), findsOneWidget);

    // A views A's profile.
    await tester.tap(find.text('Member Profile'));
    await pumpFrames(tester);
    expect(find.byType(ProfilePage), findsOneWidget);
    expect(find.text('ada@example.org'), findsOneWidget);
    expect(find.text('grace@example.org'), findsNothing);

    // Back returns to the still-ready home.
    await tester.pageBack();
    await pumpFrames(tester);
    expect(find.byType(ProfilePage), findsNothing);
    expect(find.byType(HomePage), findsOneWidget);
    expect(find.text('Ada Lovelace'), findsOneWidget);
    expect(find.text('Member Portal'), findsOneWidget);

    // --- A signs out: all authenticated member UI is gone. ---
    await tester.tap(find.byIcon(Icons.logout));
    await pumpFrames(tester);
    expect(find.byType(LoginPage), findsOneWidget);
    expect(find.byType(HomePage), findsNothing);
    expect(find.byType(ProfilePage), findsNothing);
    expect(find.text('Ada Lovelace'), findsNothing);

    // --- Account B signs in on the same app instance. ---
    when(() => memberRepo.loadMemberSession())
        .thenAnswer((_) async => _accountB());
    await signIn(
      tester,
      email: 'grace@example.org',
      password: 's3cret',
      code: '654321',
    );

    // B-only data: no stale A context, no reused member state.
    expect(find.text('Grace Hopper'), findsOneWidget);
    expect(find.text('Ada Lovelace'), findsNothing);
    expect(find.text('Lapsed'), findsWidgets);
    expect(find.text('Active'), findsNothing);
    expect(find.text('Member Portal'), findsOneWidget);

    // B's profile is keyed to B's PersonId; A's profile is never reloaded.
    await tester.tap(find.text('Member Profile'));
    await pumpFrames(tester);
    expect(find.byType(ProfilePage), findsOneWidget);
    expect(find.text('grace@example.org'), findsOneWidget);
    expect(find.text('ada@example.org'), findsNothing);
    expect(find.text('Ada Lovelace'), findsNothing);
    verify(() => memberRepo.personDetail('p2')).called(1);
    verify(() => memberRepo.personDetail('p1')).called(1);

    await tester.pumpWidget(const SizedBox());
    await tester.pump(const Duration(milliseconds: 1));
  });
}
