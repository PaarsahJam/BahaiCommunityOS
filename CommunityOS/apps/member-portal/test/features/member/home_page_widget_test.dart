import 'package:flutter/material.dart';
import 'package:flutter_bloc/flutter_bloc.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:member_portal/core/network/refresh_coordinator.dart';
import 'package:member_portal/features/auth/application/auth_bloc.dart';
import 'package:member_portal/features/auth/domain/auth_models.dart';
import 'package:member_portal/features/auth/domain/auth_repository.dart';
import 'package:member_portal/features/member/application/member_session_bloc.dart';
import 'package:member_portal/features/member/data/member_dtos.dart';
import 'package:member_portal/features/member/domain/member_models.dart';
import 'package:member_portal/features/member/domain/member_repository.dart';
import 'package:member_portal/features/member/presentation/home_page.dart';
import 'package:member_portal/features/member/presentation/member_shell.dart';
import 'package:member_portal/l10n/generated/app_localizations.dart';
import 'package:mocktail/mocktail.dart';

class _MockAuthRepository extends Mock implements AuthRepository {}

class _MockCoordinator extends Mock implements RefreshCoordinator {}

class _MockMemberRepository extends Mock implements MemberRepository {}

const _account = AuthUser(userAccountId: 'u1', email: 'ada@example.org');

PersonDto _person() => PersonDto(
      id: 'p1',
      preferredName: 'Ada',
      status: 'Active',
      hasLinkedIdentityAccount: true,
      createdOn: DateTime(2020, 1, 1),
    );

MembershipDto _membership(String status) => MembershipDto(
      id: 'm1',
      personId: 'p1',
      status: status,
      effectiveFrom: DateTime(2021, 3, 1),
    );

void main() {
  late _MockAuthRepository authRepo;
  late _MockCoordinator coordinator;
  late _MockMemberRepository repository;

  setUp(() {
    authRepo = _MockAuthRepository();
    coordinator = _MockCoordinator();
    when(() => coordinator.onSessionExpired)
        .thenAnswer((_) => const Stream.empty());
    repository = _MockMemberRepository();
    when(() => repository.loadMemberSession()).thenAnswer(
      (_) async => MemberSessionResult.resolved(
        account: _account,
        person: _person(),
        membership: null,
      ),
    );
  });

  Future<void> pumpFrames(WidgetTester tester, [int count = 16]) async {
    for (var i = 0; i < count; i++) {
      await tester.pump(const Duration(milliseconds: 100));
    }
  }

  Future<void> pumpHome(WidgetTester tester) async {
    final authBloc = AuthBloc(authRepo, coordinator);
    final sessionBloc = MemberSessionBloc(repository);
    addTearDown(authBloc.close);

    await tester.pumpWidget(
      BlocProvider<AuthBloc>.value(
        value: authBloc,
        child: MaterialApp(
          theme: ThemeData(useMaterial3: true),
          localizationsDelegates: AppLocalizations.localizationsDelegates,
          supportedLocales: AppLocalizations.supportedLocales,
          home: MemberShell(
            createSessionBloc: () => sessionBloc,
            child: const HomePage(),
          ),
        ),
      ),
    );
    await pumpFrames(tester);
  }

  group('Member home membership presentation', () {
    testWidgets('renders the ready member name and a profile affordance',
        (tester) async {
      await pumpHome(tester);

      expect(find.byType(HomePage), findsOneWidget);
      expect(find.text('Ada'), findsOneWidget);
      expect(find.text('Member Portal'), findsOneWidget);
      expect(find.text('Member Profile'), findsOneWidget);

      await tester.pumpWidget(const SizedBox());
      await tester.pump(const Duration(milliseconds: 1));
    });

    for (final status in [
      'Active',
      'Pending',
      'Suspended',
      'Lapsed',
      'Withdrawn'
    ]) {
      testWidgets('renders the "$status" membership state', (tester) async {
        when(() => repository.loadMemberSession()).thenAnswer(
          (_) async => MemberSessionResult.resolved(
            account: _account,
            person: _person(),
            membership: _membership(status),
          ),
        );
        await pumpHome(tester);

        expect(find.text('Membership'), findsOneWidget);
        expect(find.text(status), findsWidgets);
        expect(find.textContaining('Member since'), findsOneWidget);
        expect(find.text('No membership record'), findsNothing);

        await tester.pumpWidget(const SizedBox());
        await tester.pump(const Duration(milliseconds: 1));
      });
    }

    testWidgets('renders "no membership record" for a 200 + null membership',
        (tester) async {
      await pumpHome(tester);

      expect(find.text('Membership'), findsOneWidget);
      expect(find.text('No membership record'), findsOneWidget);
      expect(find.textContaining('Member since'), findsNothing);

      await tester.pumpWidget(const SizedBox());
      await tester.pump(const Duration(milliseconds: 1));
    });
  });
}
