import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:member_portal/features/auth/domain/auth_models.dart';
import 'package:member_portal/features/member/application/member_session_bloc.dart';
import 'package:member_portal/features/member/application/membership_bloc.dart';
import 'package:member_portal/features/member/data/member_dtos.dart';
import 'package:member_portal/features/member/domain/member_models.dart';
import 'package:member_portal/features/member/domain/member_repository.dart';
import 'package:member_portal/features/member/presentation/member_shell.dart';
import 'package:member_portal/features/member/presentation/membership_page.dart';
import 'package:member_portal/l10n/generated/app_localizations.dart';
import 'package:mocktail/mocktail.dart';

class _MockMemberRepository extends Mock implements MemberRepository {}

const _account = AuthUser(userAccountId: 'u1', email: 'ada@example.org');

PersonDto _person() => PersonDto(
      id: 'p1',
      preferredName: 'Ada',
      status: 'Active',
      hasLinkedIdentityAccount: true,
      createdOn: DateTime(2020, 1, 1),
    );

MembershipDto _membership({
  String status = 'Active',
  DateTime? effectiveUntil,
  DateTime? withdrawnOn,
  List<MembershipPeriodDto> history = const [],
}) =>
    MembershipDto(
      id: 'm1',
      personId: 'p1',
      status: status,
      effectiveFrom: DateTime(2021, 3, 1),
      effectiveUntil: effectiveUntil,
      withdrawnOn: withdrawnOn,
      history: history,
    );

void main() {
  late _MockMemberRepository repository;

  setUp(() {
    repository = _MockMemberRepository();
    when(() => repository.loadMemberSession()).thenAnswer(
      (_) async => MemberSessionResult.resolved(
        account: _account,
        person: _person(),
        membership: null,
      ),
    );
  });

  Future<void> pumpFrames(WidgetTester tester, [int count = 20]) async {
    for (var i = 0; i < count; i++) {
      await tester.pump(const Duration(milliseconds: 100));
    }
  }

  Future<void> pumpMembership(WidgetTester tester) async {
    final sessionBloc = MemberSessionBloc(repository);
    await tester.pumpWidget(
      MaterialApp(
        theme: ThemeData(useMaterial3: true),
        localizationsDelegates: AppLocalizations.localizationsDelegates,
        supportedLocales: AppLocalizations.supportedLocales,
        home: MemberShell(
          createSessionBloc: () => sessionBloc,
          child: MembershipPage(
            createMembershipBloc: () => MembershipBloc(repository),
          ),
        ),
      ),
    );
    await pumpFrames(tester);
  }

  testWidgets('shows a loading state while the membership is resolving',
      (tester) async {
    final gate = Completer<MembershipDto?>();
    when(() => repository.getMembership('p1')).thenAnswer((_) => gate.future);

    await pumpMembership(tester);

    expect(find.byType(CircularProgressIndicator), findsOneWidget);

    gate.complete(_membership());
    await pumpFrames(tester);

    expect(find.byType(CircularProgressIndicator), findsNothing);
    expect(find.text('Active'), findsOneWidget);
    verify(() => repository.getMembership('p1')).called(1);

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
    testWidgets('renders the "$status" membership state and member-since date',
        (tester) async {
      when(() => repository.getMembership('p1'))
          .thenAnswer((_) async => _membership(status: status));

      await pumpMembership(tester);

      expect(find.text(status), findsWidgets);
      expect(find.textContaining('Member since'), findsOneWidget);
      expect(find.text('No membership record'), findsNothing);
      verify(() => repository.getMembership('p1')).called(1);

      await tester.pumpWidget(const SizedBox());
      await tester.pump(const Duration(milliseconds: 1));
    });
  }

  testWidgets('renders the effective-until date when the backend provides it',
      (tester) async {
    when(() => repository.getMembership('p1')).thenAnswer(
      (_) async => _membership(effectiveUntil: DateTime(2024, 5, 1)),
    );

    await pumpMembership(tester);

    expect(find.text('Member since: Mar 1, 2021'), findsOneWidget);
    expect(find.text('Effective until: May 1, 2024'), findsOneWidget);

    await tester.pumpWidget(const SizedBox());
    await tester.pump(const Duration(milliseconds: 1));
  });

  testWidgets('omits the effective-until line when the backend omits it',
      (tester) async {
    when(() => repository.getMembership('p1'))
        .thenAnswer((_) async => _membership());

    await pumpMembership(tester);

    expect(find.textContaining('Effective until'), findsNothing);

    await tester.pumpWidget(const SizedBox());
    await tester.pump(const Duration(milliseconds: 1));
  });

  testWidgets('renders the withdrawn date when the backend provides it',
      (tester) async {
    when(() => repository.getMembership('p1')).thenAnswer(
      (_) async => _membership(
        status: 'Withdrawn',
        withdrawnOn: DateTime(2024, 5, 1),
      ),
    );

    await pumpMembership(tester);

    expect(find.text('Withdrawn on: May 1, 2024'), findsOneWidget);

    await tester.pumpWidget(const SizedBox());
    await tester.pump(const Duration(milliseconds: 1));
  });

  testWidgets('does not render withdrawal info when none is present',
      (tester) async {
    when(() => repository.getMembership('p1'))
        .thenAnswer((_) async => _membership());

    await pumpMembership(tester);

    expect(find.textContaining('Withdrawn on'), findsNothing);
    expect(find.text('Withdrawn'), findsNothing);

    await tester.pumpWidget(const SizedBox());
    await tester.pump(const Duration(milliseconds: 1));
  });

  testWidgets('renders the membership history timeline as supplied',
      (tester) async {
    when(() => repository.getMembership('p1')).thenAnswer(
      (_) async => _membership(
        history: [
          MembershipPeriodDto(
            status: 'pending',
            effectiveFrom: DateTime(2020, 1, 1),
            effectiveUntil: DateTime(2021, 3, 1),
          ),
          MembershipPeriodDto(
            status: 'active',
            effectiveFrom: DateTime(2021, 3, 1),
          ),
        ],
      ),
    );

    await pumpMembership(tester);

    expect(find.text('Membership history'), findsOneWidget);
    expect(find.text('Pending'), findsOneWidget);
    expect(find.text('Active'), findsWidgets);
    expect(
      find.text('Jan 1, 2020 – Mar 1, 2021'),
      findsOneWidget,
    );
    expect(find.text('From Mar 1, 2021'), findsOneWidget);

    await tester.pumpWidget(const SizedBox());
    await tester.pump(const Duration(milliseconds: 1));
  });

  testWidgets('a 200 + null renders the dedicated no-membership empty state',
      (tester) async {
    when(() => repository.getMembership('p1')).thenAnswer((_) async => null);

    await pumpMembership(tester);

    expect(find.text('Membership'), findsOneWidget);
    expect(find.text('No membership record'), findsOneWidget);
    expect(
      find.text('You do not currently have a membership record.'),
      findsOneWidget,
    );
    expect(find.text('Active'), findsNothing);
    expect(find.textContaining('Member since'), findsNothing);
    expect(find.text('Try again'), findsNothing);

    await tester.pumpWidget(const SizedBox());
    await tester.pump(const Duration(milliseconds: 1));
  });

  testWidgets('a 403 renders the localized forbidden state, not an empty one',
      (tester) async {
    when(() => repository.getMembership('p1'))
        .thenThrow(const ForbiddenException('nope'));

    await pumpMembership(tester);

    expect(
      find.text('Your membership information cannot be viewed at this time.'),
      findsOneWidget,
    );
    expect(find.text('Try again'), findsOneWidget);
    expect(find.text('No membership record'), findsNothing);
    expect(find.byType(CircularProgressIndicator), findsNothing);

    await tester.pumpWidget(const SizedBox());
    await tester.pump(const Duration(milliseconds: 1));
  });

  testWidgets('a 404 renders a not-found failure, never the empty state',
      (tester) async {
    when(() => repository.getMembership('p1'))
        .thenThrow(const NotFoundException('missing'));

    await pumpMembership(tester);

    expect(
      find.text('This membership information could not be found.'),
      findsOneWidget,
    );
    expect(find.text('Try again'), findsOneWidget);
    expect(find.text('No membership record'), findsNothing);

    await tester.pumpWidget(const SizedBox());
    await tester.pump(const Duration(milliseconds: 1));
  });

  testWidgets('a network failure is retryable and a retry reloads membership',
      (tester) async {
    when(() => repository.getMembership('p1'))
        .thenThrow(const NetworkException('offline'));

    await pumpMembership(tester);

    expect(find.text('offline'), findsOneWidget);
    expect(find.text('Try again'), findsOneWidget);
    expect(find.text('No membership record'), findsNothing);
    expect(find.byType(CircularProgressIndicator), findsNothing);

    when(() => repository.getMembership('p1'))
        .thenAnswer((_) async => _membership());

    await tester.tap(find.text('Try again'));
    await pumpFrames(tester);

    expect(find.text('Active'), findsOneWidget);
    expect(find.byType(CircularProgressIndicator), findsNothing);
    verify(() => repository.getMembership('p1')).called(2);

    await tester.pumpWidget(const SizedBox());
    await tester.pump(const Duration(milliseconds: 1));
  });

  testWidgets(
      'a server error is retryable and never becomes the empty or forbidden '
      'state', (tester) async {
    when(() => repository.getMembership('p1')).thenThrow(
      const ServerException('boom', statusCode: 500),
    );

    await pumpMembership(tester);

    expect(find.text('boom'), findsOneWidget);
    expect(find.text('Try again'), findsOneWidget);
    expect(find.text('No membership record'), findsNothing);
    expect(
      find.text('Your membership information cannot be viewed at this time.'),
      findsNothing,
    );

    await tester.pumpWidget(const SizedBox());
    await tester.pump(const Duration(milliseconds: 1));
  });

  testWidgets('the membership request always targets the session person',
      (tester) async {
    when(() => repository.getMembership('p1'))
        .thenAnswer((_) async => _membership());

    await pumpMembership(tester);

    verify(() => repository.getMembership('p1')).called(1);

    await tester.pumpWidget(const SizedBox());
    await tester.pump(const Duration(milliseconds: 1));
  });
}
