import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter_bloc/flutter_bloc.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:member_portal/core/error/app_exception.dart';
import 'package:member_portal/core/network/refresh_coordinator.dart';
import 'package:member_portal/features/account/application/account_bloc.dart';
import 'package:member_portal/features/account/application/security_bloc.dart';
import 'package:member_portal/features/account/presentation/account_page.dart';
import 'package:member_portal/features/auth/application/auth_bloc.dart';
import 'package:member_portal/features/auth/data/auth_dtos.dart';
import 'package:member_portal/features/auth/domain/auth_repository.dart';
import 'package:member_portal/features/member/domain/member_repository.dart';
import 'package:member_portal/l10n/generated/app_localizations.dart';
import 'package:mocktail/mocktail.dart';

class _MockMemberRepository extends Mock implements MemberRepository {}

class _MockAuthRepository extends Mock implements AuthRepository {}

class _MockCoordinator extends Mock implements RefreshCoordinator {}

UserAccountDto _account() => UserAccountDto(
      id: 'u1',
      email: 'ada@example.org',
      status: 'Active',
      createdOn: DateTime(2020, 1, 1),
    );

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
  late _MockMemberRepository repository;
  late _MockAuthRepository authRepo;
  late _MockCoordinator coordinator;

  setUp(() {
    repository = _MockMemberRepository();
    authRepo = _MockAuthRepository();
    coordinator = _MockCoordinator();
    when(() => coordinator.onSessionExpired)
        .thenAnswer((_) => const Stream.empty());
    when(() => authRepo.clearLocalAuth()).thenAnswer((_) async {});
  });

  Future<void> pumpFrames(WidgetTester tester, [int count = 30]) async {
    for (var i = 0; i < count; i++) {
      await tester.pump(const Duration(milliseconds: 100));
    }
  }

  Future<void> pumpHarness(
    WidgetTester tester, {
    double textScale = 1.0,
  }) async {
    tester.view.physicalSize = const Size(800, 2400);
    tester.view.devicePixelRatio = 1.0;
    addTearDown(tester.view.reset);
    final authBloc = AuthBloc(authRepo, coordinator);
    addTearDown(authBloc.close);
    await tester.pumpWidget(
      MaterialApp(
        theme: ThemeData(useMaterial3: true),
        localizationsDelegates: AppLocalizations.localizationsDelegates,
        supportedLocales: AppLocalizations.supportedLocales,
        builder: (context, child) => MediaQuery(
          data: MediaQuery.of(context).copyWith(
            textScaler: TextScaler.linear(textScale),
          ),
          child: child!,
        ),
        home: BlocProvider<AuthBloc>.value(
          value: authBloc,
          child: AccountPage(
            createAccountBloc: () => AccountBloc(repository),
            createSecurityBloc: () => SecurityBloc(repository),
          ),
        ),
      ),
    );
    await pumpFrames(tester);
  }

  void stubDefaults() {
    when(() => repository.loadAccount()).thenAnswer((_) async => _account());
    when(() => repository.securityEvents())
        .thenAnswer((_) async => <SecurityEventDto>[]);
    when(() => repository.sessions()).thenAnswer((_) async => <SessionDto>[]);
  }

  testWidgets(
      'the revoke-others action is visible in the session-management section',
      (tester) async {
    stubDefaults();
    when(() => repository.sessions()).thenAnswer((_) async => [
          _session(),
          _session(id: 's2', deviceId: 'd2'),
        ]);

    await pumpHarness(tester);

    expect(find.byKey(const Key('session-revoke-others')), findsOneWidget);
    expect(find.text('Revoke all other sessions'), findsOneWidget);
    // The per-session revoke controls remain untouched.
    expect(find.byKey(const Key('session-revoke-s1')), findsOneWidget);
    expect(find.byKey(const Key('session-revoke-s2')), findsOneWidget);

    await tester.pumpWidget(const SizedBox());
    await tester.pump(const Duration(milliseconds: 1));
  });

  testWidgets(
      'the confirmation dialog explains the scope and cancel never invokes '
      'the API', (tester) async {
    stubDefaults();
    when(() => repository.sessions()).thenAnswer((_) async => [
          _session(),
          _session(id: 's2', deviceId: 'd2'),
        ]);

    await pumpHarness(tester);

    await tester.tap(find.byKey(const Key('session-revoke-others')));
    await pumpFrames(tester);

    expect(find.text('Revoke all other sessions?'), findsOneWidget);
    expect(
      find.text(
        'All other signed-in sessions will be revoked. Your current session '
        'will remain active. Already-issued access tokens from other sessions '
        'may remain valid for a short time.',
      ),
      findsOneWidget,
    );

    await tester.tap(find.text('Cancel'));
    await pumpFrames(tester);

    expect(find.text('Revoke all other sessions?'), findsNothing);
    verifyNever(() => repository.revokeOtherSessions());

    await tester.pumpWidget(const SizedBox());
    await tester.pump(const Duration(milliseconds: 1));
  });

  testWidgets(
      'confirming revokes other sessions, shows the success banner, and '
      'reloads the list preserving the current session', (tester) async {
    stubDefaults();
    var readCount = 0;
    when(() => repository.sessions()).thenAnswer((_) async {
      readCount++;
      if (readCount == 1) {
        return [_session(), _session(id: 's2', deviceId: 'd2')];
      }
      // The backend preserves the current family; only the current session
      // remains after the authoritative reload.
      return [_session()];
    });
    when(() => repository.revokeOtherSessions()).thenAnswer((_) async {});

    await pumpHarness(tester);

    await tester.tap(find.byKey(const Key('session-revoke-others')));
    await pumpFrames(tester);
    await tester.tap(find.byKey(const Key('session-revoke-others-confirm')));
    await pumpFrames(tester);

    verify(() => repository.revokeOtherSessions()).called(1);
    expect(find.text('Other sessions revoked.'), findsOneWidget);
    expect(find.byKey(const Key('session-revoke-s1')), findsOneWidget);
    expect(find.byKey(const Key('session-revoke-s2')), findsNothing);
    // The success banner is announced as a live region.
    expect(
      find.byWidgetPredicate((widget) =>
          widget is Semantics && widget.properties.liveRegion == true),
      findsWidgets,
    );
    // The action stays available for a later run.
    expect(find.byKey(const Key('session-revoke-others')), findsOneWidget);

    await tester.pumpWidget(const SizedBox());
    await tester.pump(const Duration(milliseconds: 1));
  });

  testWidgets(
      'while revoke-others is in flight the action is disabled and announced',
      (tester) async {
    stubDefaults();
    when(() => repository.sessions()).thenAnswer((_) async => [
          _session(),
          _session(id: 's2', deviceId: 'd2'),
        ]);
    final gate = Completer<void>();
    when(() => repository.revokeOtherSessions())
        .thenAnswer((_) => gate.future);

    await pumpHarness(tester);

    await tester.tap(find.byKey(const Key('session-revoke-others')));
    await pumpFrames(tester);
    await tester.tap(find.byKey(const Key('session-revoke-others-confirm')));
    await tester.pump(const Duration(milliseconds: 100));

    // The action is replaced by the announced in-flight progress, so a second
    // submission cannot even be expressed.
    expect(find.byKey(const Key('session-revoke-others')), findsNothing);
    expect(
      find.byWidgetPredicate((widget) =>
          widget is Semantics &&
          widget.properties.label == 'Revoking other sessions…'),
      findsOneWidget,
    );
    expect(find.byType(CircularProgressIndicator), findsOneWidget);

    gate.complete();
    await pumpFrames(tester);

    verify(() => repository.revokeOtherSessions()).called(1);
    expect(find.text('Other sessions revoked.'), findsOneWidget);

    await tester.pumpWidget(const SizedBox());
    await tester.pump(const Duration(milliseconds: 1));
  });

  testWidgets(
      'a failed revoke-others keeps the operation retryable and never reports '
      'success', (tester) async {
    stubDefaults();
    when(() => repository.sessions()).thenAnswer((_) async => [
          _session(),
          _session(id: 's2', deviceId: 'd2'),
        ]);
    var attempts = 0;
    when(() => repository.revokeOtherSessions()).thenAnswer((_) async {
      attempts++;
      if (attempts == 1) {
        throw const NotFoundException(
          'This session belonged to another account.',
          statusCode: 404,
        );
      }
    });

    await pumpHarness(tester);

    await tester.tap(find.byKey(const Key('session-revoke-others')));
    await pumpFrames(tester);
    await tester.tap(find.byKey(const Key('session-revoke-others-confirm')));
    await pumpFrames(tester);

    // Generic failure: the backend's 404 detail is never echoed.
    expect(find.text('The other sessions could not be revoked.'), findsOneWidget);
    expect(find.textContaining('another account'), findsNothing);
    expect(find.text('Other sessions revoked.'), findsNothing);
    expect(find.byKey(const Key('session-revoke-others-retry')), findsOneWidget);
    // The session list is untouched by a failed operation.
    expect(find.byKey(const Key('session-revoke-s1')), findsOneWidget);
    expect(find.byKey(const Key('session-revoke-s2')), findsOneWidget);

    await tester.tap(find.byKey(const Key('session-revoke-others-retry')));
    await pumpFrames(tester);

    expect(find.text('Other sessions revoked.'), findsOneWidget);
    expect(find.byKey(const Key('session-revoke-others-retry')), findsNothing);
    verify(() => repository.revokeOtherSessions()).called(2);

    await tester.pumpWidget(const SizedBox());
    await tester.pump(const Duration(milliseconds: 1));
  });

  testWidgets('a transient revoke-others failure stays retryable',
      (tester) async {
    stubDefaults();
    when(() => repository.sessions()).thenAnswer((_) async => [
          _session(),
          _session(id: 's2', deviceId: 'd2'),
        ]);
    when(() => repository.revokeOtherSessions())
        .thenThrow(const NetworkException('offline'));

    await pumpHarness(tester);

    await tester.tap(find.byKey(const Key('session-revoke-others')));
    await pumpFrames(tester);
    await tester.tap(find.byKey(const Key('session-revoke-others-confirm')));
    await pumpFrames(tester);

    expect(
      find.text('The other sessions could not be revoked. Please try again.'),
      findsOneWidget,
    );
    expect(find.byKey(const Key('session-revoke-others-retry')), findsOneWidget);
    expect(find.text('Other sessions revoked.'), findsNothing);

    await tester.pumpWidget(const SizedBox());
    await tester.pump(const Duration(milliseconds: 1));
  });

  testWidgets('a 401 revoke-others failure is never reported as success',
      (tester) async {
    stubDefaults();
    when(() => repository.sessions()).thenAnswer((_) async => [
          _session(),
          _session(id: 's2', deviceId: 'd2'),
        ]);
    when(() => repository.revokeOtherSessions()).thenThrow(
      const UnauthorizedException('token invalid', statusCode: 401),
    );

    await pumpHarness(tester);

    await tester.tap(find.byKey(const Key('session-revoke-others')));
    await pumpFrames(tester);
    await tester.tap(find.byKey(const Key('session-revoke-others-confirm')));
    await pumpFrames(tester);

    expect(find.text('Other sessions revoked.'), findsNothing);
    expect(
      find.text('The other sessions could not be revoked. Please try again.'),
      findsOneWidget,
    );

    await tester.pumpWidget(const SizedBox());
    await tester.pump(const Duration(milliseconds: 1));
  });

  testWidgets('revoke-others coexists with the unchanged per-session revoke',
      (tester) async {
    stubDefaults();
    when(() => repository.sessions()).thenAnswer((_) async => [
          _session(),
          _session(id: 's2', deviceId: 'd2', isActive: false),
        ]);

    await pumpHarness(tester);

    // Only the active other session exposes the per-session revoke control.
    expect(find.byKey(const Key('session-revoke-s1')), findsOneWidget);
    expect(find.byKey(const Key('session-revoke-s2')), findsNothing);
    expect(find.byKey(const Key('session-revoke-others')), findsOneWidget);

    // The existing per-session dialog still gates that mutation.
    await tester.tap(find.byKey(const Key('session-revoke-s1')));
    await pumpFrames(tester);
    expect(find.text('Revoke this session?'), findsOneWidget);
    await tester.tap(find.text('Cancel'));
    await pumpFrames(tester);
    verifyNever(() => repository.revokeSession(any()));

    await tester.pumpWidget(const SizedBox());
    await tester.pump(const Duration(milliseconds: 1));
  });

  testWidgets('revoke-others scales to large text without overflow',
      (tester) async {
    stubDefaults();
    when(() => repository.sessions()).thenAnswer((_) async => [
          _session(),
          _session(id: 's2', deviceId: 'd2'),
        ]);

    await pumpHarness(tester, textScale: 2.0);

    expect(tester.takeException(), isNull);
    expect(find.byKey(const Key('session-revoke-others')), findsOneWidget);

    await tester.pumpWidget(const SizedBox());
    await tester.pump(const Duration(milliseconds: 1));
  });
}