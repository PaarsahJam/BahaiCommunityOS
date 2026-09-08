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
      'only active sessions expose revoke and the confirm dialog gates the '
      'mutation', (tester) async {
    stubDefaults();
    var readCount = 0;
    when(() => repository.sessions()).thenAnswer((_) async {
      readCount++;
      if (readCount == 1) {
        return [
          _session(),
          _session(id: 's2', deviceId: 'd2', isActive: false),
        ];
      }
      return <SessionDto>[];
    });
    when(() => repository.revokeSession('s1')).thenAnswer((_) async {});

    await pumpHarness(tester);

    // Only the active session offers the revoke control.
    expect(find.byKey(const Key('session-revoke-s1')), findsOneWidget);
    expect(find.byKey(const Key('session-revoke-s2')), findsNothing);

    // Cancel closes the dialog and never issues a request.
    await tester.tap(find.byKey(const Key('session-revoke-s1')));
    await pumpFrames(tester);
    expect(find.text('Revoke this session?'), findsOneWidget);
    await tester.tap(find.text('Cancel'));
    await pumpFrames(tester);
    expect(find.text('Revoke this session?'), findsNothing);
    verifyNever(() => repository.revokeSession(any()));

    // Confirming revokes; the row then disappears via the authoritative reload.
    await tester.tap(find.byKey(const Key('session-revoke-s1')));
    await pumpFrames(tester);
    await tester.tap(find.byKey(const Key('session-revoke-confirm')));
    await pumpFrames(tester);

    verify(() => repository.revokeSession('s1')).called(1);
    expect(find.text('Session revoked.'), findsOneWidget);
    // The success banner is announced as a live region.
    expect(
      find.byWidgetPredicate((widget) =>
          widget is Semantics && widget.properties.liveRegion == true),
      findsOneWidget,
    );
    expect(find.byKey(const Key('session-revoke-s1')), findsNothing);

    await tester.pumpWidget(const SizedBox());
    await tester.pump(const Duration(milliseconds: 1));
  });

  testWidgets(
      'while a revoke is in flight the row is disabled and announced, and a '
      'repeated attempt is suppressed', (tester) async {
    stubDefaults();
    var readCount = 0;
    when(() => repository.sessions()).thenAnswer((_) async {
      readCount++;
      if (readCount == 1) return [_session()];
      return <SessionDto>[];
    });
    final gate = Completer<void>();
    when(() => repository.revokeSession('s1')).thenAnswer((_) => gate.future);

    await pumpHarness(tester);

    await tester.tap(find.byKey(const Key('session-revoke-s1')));
    await pumpFrames(tester);
    await tester.tap(find.byKey(const Key('session-revoke-confirm')));
    await tester.pump(const Duration(milliseconds: 100));

    // The row is no longer tappable: the revoke control is replaced by the
    // in-flight progress, announced to assistive technology.
    expect(find.byKey(const Key('session-revoke-s1')), findsNothing);
    expect(
      find.byWidgetPredicate((widget) =>
          widget is Semantics &&
          widget.properties.label == 'Revoking this session…'),
      findsOneWidget,
    );
    expect(find.byType(CircularProgressIndicator), findsOneWidget);

    gate.complete();
    await pumpFrames(tester);

    verify(() => repository.revokeSession('s1')).called(1);
    expect(find.text('Session revoked.'), findsOneWidget);
    expect(find.byKey(const Key('session-revoke-s1')), findsNothing);

    await tester.pumpWidget(const SizedBox());
    await tester.pump(const Duration(milliseconds: 1));
  });

  testWidgets(
      'a failed revoke keeps the session visible with a generic message and '
      'the retry completes', (tester) async {
    stubDefaults();
    when(() => repository.sessions()).thenAnswer((_) async => [_session()]);
    var attempts = 0;
    when(() => repository.revokeSession('s1')).thenAnswer((_) async {
      attempts++;
      if (attempts == 1) {
        throw const NotFoundException(
          'This session belonged to another account.',
          statusCode: 404,
        );
      }
    });

    await pumpHarness(tester);

    await tester.tap(find.byKey(const Key('session-revoke-s1')));
    await pumpFrames(tester);
    await tester.tap(find.byKey(const Key('session-revoke-confirm')));
    await pumpFrames(tester);

    // Generic failure: the backend's per-session 404 detail is never echoed,
    // and the session stays visible and retryable in place.
    expect(find.text('This session could not be revoked.'), findsOneWidget);
    expect(find.textContaining('another account'), findsNothing);
    expect(find.text('Session revoked.'), findsNothing);
    expect(find.byKey(const Key('session-revoke-retry-s1')), findsOneWidget);
    expect(find.byKey(const Key('session-revoke-s1')), findsNothing);

    await tester.tap(find.byKey(const Key('session-revoke-retry-s1')));
    await pumpFrames(tester);

    expect(find.text('Session revoked.'), findsOneWidget);
    expect(find.byKey(const Key('session-revoke-retry-s1')), findsNothing);
    verify(() => repository.revokeSession('s1')).called(2);

    await tester.pumpWidget(const SizedBox());
    await tester.pump(const Duration(milliseconds: 1));
  });

  testWidgets('a transient revoke failure stays retryable', (tester) async {
    stubDefaults();
    when(() => repository.sessions()).thenAnswer((_) async => [_session()]);
    when(() => repository.revokeSession('s1'))
        .thenThrow(const NetworkException('offline'));

    await pumpHarness(tester);

    await tester.tap(find.byKey(const Key('session-revoke-s1')));
    await pumpFrames(tester);
    await tester.tap(find.byKey(const Key('session-revoke-confirm')));
    await pumpFrames(tester);

    expect(find.text('This session could not be revoked. Please try again.'),
        findsOneWidget);
    expect(find.byKey(const Key('session-revoke-retry-s1')), findsOneWidget);
    expect(find.text('Session revoked.'), findsNothing);

    await tester.pumpWidget(const SizedBox());
    await tester.pump(const Duration(milliseconds: 1));
  });

  testWidgets('the revoke action exposes an accessible tooltip',
      (tester) async {
    stubDefaults();
    when(() => repository.sessions()).thenAnswer((_) async => [_session()]);

    await pumpHarness(tester);

    expect(find.byTooltip('Revoke this session'), findsOneWidget);
    expect(find.byKey(const Key('session-revoke-s1')), findsOneWidget);

    await tester.pumpWidget(const SizedBox());
    await tester.pump(const Duration(milliseconds: 1));
  });

  testWidgets('the revoke control scales to large text without overflow',
      (tester) async {
    stubDefaults();
    when(() => repository.sessions()).thenAnswer((_) async => [_session()]);

    await pumpHarness(tester, textScale: 2.0);

    expect(tester.takeException(), isNull);
    expect(find.byKey(const Key('session-revoke-s1')), findsOneWidget);

    await tester.pumpWidget(const SizedBox());
    await tester.pump(const Duration(milliseconds: 1));
  });
}
