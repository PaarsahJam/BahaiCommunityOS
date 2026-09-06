import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter_bloc/flutter_bloc.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:member_portal/core/network/refresh_coordinator.dart';
import 'package:member_portal/core/router/app_router.dart';
import 'package:member_portal/core/storage/token_storage.dart';
import 'package:member_portal/features/auth/application/auth_bloc.dart';
import 'package:member_portal/features/auth/application/auth_event.dart';
import 'package:member_portal/features/auth/domain/auth_models.dart';
import 'package:member_portal/features/auth/domain/auth_repository.dart';
import 'package:member_portal/features/member/application/member_session_bloc.dart';
import 'package:member_portal/features/member/application/member_session_event.dart';
import 'package:member_portal/features/member/application/member_session_state.dart';
import 'package:member_portal/features/member/application/profile_bloc.dart';
import 'package:member_portal/features/member/data/member_dtos.dart';
import 'package:member_portal/features/member/domain/member_models.dart';
import 'package:member_portal/features/member/domain/member_repository.dart';
import 'package:member_portal/features/member/presentation/home_page.dart';
import 'package:member_portal/features/member/presentation/member_shell.dart';
import 'package:member_portal/features/member/presentation/profile_page.dart';
import 'package:member_portal/l10n/generated/app_localizations.dart';
import 'package:mocktail/mocktail.dart';

class _MockAuthRepository extends Mock implements AuthRepository {}

class _MockCoordinator extends Mock implements RefreshCoordinator {}

class _MockMemberRepository extends Mock implements MemberRepository {}

const _sessionChild = Text('SHELL_CHILD');
const _accountA = AuthUser(userAccountId: 'u1', email: 'ada@example.org');

Future<void> pumpFrames(WidgetTester tester, [int count = 16]) async {
  for (var i = 0; i < count; i++) {
    await tester.pump(const Duration(milliseconds: 100));
  }
}

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

  Widget shellHarness(AuthBloc authBloc, MemberSessionBloc sessionBloc) {
    return BlocProvider<AuthBloc>.value(
      value: authBloc,
      child: MaterialApp(
        theme: ThemeData(useMaterial3: true),
        localizationsDelegates: AppLocalizations.localizationsDelegates,
        supportedLocales: AppLocalizations.supportedLocales,
        home: MemberShell(
          child: _sessionChild,
          createSessionBloc: () => sessionBloc,
        ),
      ),
    );
  }

  group('MemberShell bootstrap gate', () {
    testWidgets('shows a progress indicator while bootstrapping',
        (tester) async {
      final gate = Completer<MemberSessionResult>();
      when(() => memberRepo.loadMemberSession()).thenAnswer((_) => gate.future);

      final authBloc = AuthBloc(authRepo, coordinator);
      final sessionBloc = MemberSessionBloc(memberRepo);
      addTearDown(authBloc.close);

      await tester.pumpWidget(shellHarness(authBloc, sessionBloc));
      await tester.pump(const Duration(milliseconds: 100));

      expect(find.byType(CircularProgressIndicator), findsWidgets);
      expect(find.text('SHELL_CHILD'), findsNothing);

      gate.complete(
        MemberSessionResult.resolved(
          account: _accountA,
          person: _person('p1', 'Ada'),
          membership: _membership(),
        ),
      );
      await pumpFrames(tester);

      expect(find.text('SHELL_CHILD'), findsOneWidget);

      await tester.pumpWidget(const SizedBox());
      await tester.pump(const Duration(milliseconds: 1));
    });

    testWidgets('renders the unlinked view for a 404 my-person outcome',
        (tester) async {
      when(() => memberRepo.loadMemberSession())
          .thenAnswer((_) async => const MemberSessionResult.unlinked());

      final authBloc = AuthBloc(authRepo, coordinator);
      final sessionBloc = MemberSessionBloc(memberRepo);
      addTearDown(authBloc.close);

      await tester.pumpWidget(shellHarness(authBloc, sessionBloc));
      await pumpFrames(tester);

      expect(find.text('Profile not linked'), findsOneWidget);
      expect(find.text('SHELL_CHILD'), findsNothing);

      await tester.pumpWidget(const SizedBox());
      await tester.pump(const Duration(milliseconds: 1));
    });

    testWidgets('renders a keyed message for a forbidden outcome',
        (tester) async {
      when(() => memberRepo.loadMemberSession()).thenAnswer(
        (_) async => const MemberSessionResult.forbidden(
          ForbiddenException('nope', messageKey: 'profile_forbidden'),
        ),
      );

      final authBloc = AuthBloc(authRepo, coordinator);
      final sessionBloc = MemberSessionBloc(memberRepo);
      addTearDown(authBloc.close);

      await tester.pumpWidget(shellHarness(authBloc, sessionBloc));
      await pumpFrames(tester);

      expect(
        find.text("You don't have permission to view this profile."),
        findsOneWidget,
      );
      expect(find.text('SHELL_CHILD'), findsNothing);

      await tester.pumpWidget(const SizedBox());
      await tester.pump(const Duration(milliseconds: 1));
    });

    testWidgets('renders an error view for a network failure', (tester) async {
      when(() => memberRepo.loadMemberSession()).thenAnswer(
        (_) async =>
            const MemberSessionResult.failed(NetworkException('offline')),
      );

      final authBloc = AuthBloc(authRepo, coordinator);
      final sessionBloc = MemberSessionBloc(memberRepo);
      addTearDown(authBloc.close);

      await tester.pumpWidget(shellHarness(authBloc, sessionBloc));
      await pumpFrames(tester);

      expect(find.byIcon(Icons.error_outline), findsOneWidget);
      expect(find.text('SHELL_CHILD'), findsNothing);

      await tester.pumpWidget(const SizedBox());
      await tester.pump(const Duration(milliseconds: 1));
    });

    testWidgets('shows a progress indicator while the session self-heals',
        (tester) async {
      when(() => memberRepo.loadMemberSession())
          .thenAnswer((_) async => const MemberSessionResult.unauthenticated());

      final authBloc = AuthBloc(authRepo, coordinator);
      final sessionBloc = MemberSessionBloc(memberRepo);
      addTearDown(authBloc.close);

      await tester.pumpWidget(shellHarness(authBloc, sessionBloc));
      await pumpFrames(tester);

      expect(find.byType(CircularProgressIndicator), findsWidgets);
      expect(find.text('SHELL_CHILD'), findsNothing);

      await tester.pumpWidget(const SizedBox());
      await tester.pump(const Duration(milliseconds: 1));
    });

    testWidgets('retry re-requests the bootstrap and can reach the child',
        (tester) async {
      var calls = 0;
      when(() => memberRepo.loadMemberSession()).thenAnswer((_) async {
        calls += 1;
        if (calls == 1) {
          return const MemberSessionResult.failed(
            NetworkException('offline'),
          );
        }
        return MemberSessionResult.resolved(
          account: _accountA,
          person: _person('p1', 'Ada'),
          membership: _membership(),
        );
      });

      final authBloc = AuthBloc(authRepo, coordinator);
      final sessionBloc = MemberSessionBloc(memberRepo);
      addTearDown(authBloc.close);

      await tester.pumpWidget(shellHarness(authBloc, sessionBloc));
      await pumpFrames(tester);

      expect(find.text('SHELL_CHILD'), findsNothing);
      expect(find.text('Try again'), findsOneWidget);

      await tester.tap(find.text('Try again'));
      await pumpFrames(tester);

      expect(calls, 2);
      expect(find.text('SHELL_CHILD'), findsOneWidget);

      await tester.pumpWidget(const SizedBox());
      await tester.pump(const Duration(milliseconds: 1));
    });

    testWidgets(
        'a shell disposed while the bootstrap is in flight never emits or '
        'hangs', (tester) async {
      final gate = Completer<MemberSessionResult>();
      when(() => memberRepo.loadMemberSession()).thenAnswer((_) => gate.future);

      final authBloc = AuthBloc(authRepo, coordinator);
      final sessionBloc = MemberSessionBloc(memberRepo);
      addTearDown(authBloc.close);

      await tester.pumpWidget(shellHarness(authBloc, sessionBloc));
      await tester.pump(const Duration(milliseconds: 100));

      expect(find.byType(CircularProgressIndicator), findsWidgets);
      expect(sessionBloc.isClosed, isFalse);

      // Dispose the whole shell (sign-out / session expiry) with the
      // bootstrap request still unresolved.
      await tester.pumpWidget(const SizedBox());
      await tester.pump(const Duration(milliseconds: 1));

      // The bloc is closed: it rejects any further event.
      expect(
        () => sessionBloc.add(const MemberSessionEvent.bootstrapRequested()),
        throwsStateError,
      );

      // The late result, whatever it is, must never surface: the shell (and
      // its published context) is already gone. The bloc stays in its last
      // published state (bootstrap loading) and never transitions.
      gate.complete(
        MemberSessionResult.resolved(
          account: _accountA,
          person: _person('p1', 'Ada'),
          membership: _membership(),
        ),
      );
      await pumpFrames(tester, 4);

      expect(sessionBloc.state, const MemberSessionState.loading());
      expect(find.text('SHELL_CHILD'), findsNothing);
    });
  });

  group('MemberShell routing', () {
    testWidgets(
        'home renders the bootstrapped context and navigates to the '
        'profile then back', (tester) async {
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
      when(() => memberRepo.loadMemberSession()).thenAnswer(
        (_) async => MemberSessionResult.resolved(
          account: _accountA,
          person: _person('p1', 'Ada'),
          membership: _membership(status: 'Active'),
        ),
      );
      when(() => memberRepo.personDetail('p1')).thenAnswer(
        (_) async => PersonDetailDto(
          id: 'p1',
          preferredName: 'Ada',
          status: 'Active',
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
        ),
      );

      final authBloc = AuthBloc(authRepo, coordinator);
      final sessionBloc = MemberSessionBloc(memberRepo);
      addTearDown(authBloc.close);
      final router = AppRouter.build(
        authBloc,
        createMemberSession: () => sessionBloc,
        createProfile: () => ProfileBloc(memberRepo),
      );

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

      expect(find.byType(HomePage), findsOneWidget);
      expect(find.text('Ada'), findsOneWidget);
      expect(find.text('Member Portal'), findsOneWidget);
      verifyNever(() => memberRepo.personDetail(any()));

      await tester.tap(find.text('Member Profile'));
      await pumpFrames(tester);

      expect(find.byType(ProfilePage), findsOneWidget);
      verify(() => memberRepo.personDetail('p1')).called(1);
      expect(find.text('Contact methods'), findsOneWidget);

      await tester.pageBack();
      await pumpFrames(tester);

      expect(find.byType(ProfilePage), findsNothing);
      expect(find.byType(HomePage), findsOneWidget);
      expect(find.text('Member Portal'), findsOneWidget);
      expect(find.text('Ada'), findsOneWidget);

      await tester.pumpWidget(const SizedBox());
      await tester.pump(const Duration(milliseconds: 1));
    });

    testWidgets(
        'repeated Profile navigation uses an independent profile lifecycle '
        'per visit', (tester) async {
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
      when(() => memberRepo.loadMemberSession()).thenAnswer(
        (_) async => MemberSessionResult.resolved(
          account: _accountA,
          person: _person('p1', 'Ada'),
          membership: _membership(status: 'Active'),
        ),
      );
      var personCalls = 0;
      when(() => memberRepo.personDetail('p1')).thenAnswer((_) async {
        personCalls += 1;
        return _fullDetail();
      });

      final authBloc = AuthBloc(authRepo, coordinator);
      final sessionBloc = MemberSessionBloc(memberRepo);
      addTearDown(authBloc.close);
      final router = AppRouter.build(
        authBloc,
        createMemberSession: () => sessionBloc,
        createProfile: () => ProfileBloc(memberRepo),
      );

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

      expect(find.byType(HomePage), findsOneWidget);
      expect(personCalls, 0);

      await tester.tap(find.text('Member Profile'));
      await pumpFrames(tester);
      expect(find.byType(ProfilePage), findsOneWidget);
      expect(personCalls, 1);

      await tester.pageBack();
      await pumpFrames(tester);
      expect(find.byType(ProfilePage), findsNothing);
      expect(find.byType(HomePage), findsOneWidget);

      // The second visit constructs a fresh ProfileBloc; exactly one request
      // per visit, no reuse of the previous bloc's state.
      await tester.tap(find.text('Member Profile'));
      await pumpFrames(tester);
      expect(find.byType(ProfilePage), findsOneWidget);
      expect(personCalls, 2);
      expect(find.text('Contact methods'), findsOneWidget);

      await tester.pumpWidget(const SizedBox());
      await tester.pump(const Duration(milliseconds: 1));
    });
  });
}

PersonDto _person(String id, String name) => PersonDto(
      id: id,
      preferredName: name,
      status: 'Active',
      hasLinkedIdentityAccount: true,
      createdOn: DateTime(2020, 1, 1),
    );

MembershipDto _membership({String status = 'Pending'}) => MembershipDto(
      id: 'm1',
      personId: 'p1',
      status: status,
      effectiveFrom: DateTime(2021, 3, 1),
    );

PersonDetailDto _fullDetail() => PersonDetailDto(
      id: 'p1',
      preferredName: 'Ada',
      formalName: 'Ada Lovelace',
      status: 'Active',
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
