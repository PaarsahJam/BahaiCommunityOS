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

UserAccountDto _account({
  List<MfaMethodDto> mfaMethods = const [],
  List<DeviceDto> devices = const [],
}) =>
    UserAccountDto(
      id: 'u1',
      email: 'ada@example.org',
      status: 'Active',
      createdOn: DateTime(2020, 1, 1),
      mfaMethods: mfaMethods,
      devices: devices,
    );

UserAccountDto _mfaEnabledAccount() => _account(
      mfaMethods: [
        MfaMethodDto(
          id: 'm1',
          type: 'Totp',
          isVerified: true,
          isActive: true,
          createdOn: DateTime(2021, 1, 1),
        ),
      ],
    );

MfaEnrollmentDto _enrollment({String methodId = 'm1'}) => MfaEnrollmentDto(
      mfaMethodId: methodId,
      secret: 'BASE32SECRET',
      provisioningUri: 'otpauth://totp/CommunityOS:ada?secret=BASE32SECRET',
    );

SessionDto _session({
  String id = 's1',
  String deviceId = 'd1',
  String? deviceName,
  String? devicePlatform,
  bool isActive = true,
}) =>
    SessionDto(
      id: id,
      deviceId: deviceId,
      deviceName: deviceName,
      devicePlatform: devicePlatform,
      createdOn: DateTime(2026, 9, 1, 9),
      expiresOn: DateTime(2026, 9, 8, 9),
      lastUsedOn: DateTime(2026, 9, 7, 12),
      isActive: isActive,
    );

DeviceDto _device({
  String id = 'd1',
  String name = 'Back office terminal',
  bool isTrusted = true,
}) =>
    DeviceDto(
      id: id,
      name: name,
      platform: 'Windows',
      registeredOn: DateTime(2024, 5, 1),
      isTrusted: isTrusted,
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

  late AuthBloc authBloc;

  Future<void> pumpHarness(
    WidgetTester tester, {
    double textScale = 1.0,
  }) async {
    tester.view.physicalSize = const Size(800, 2400);
    tester.view.devicePixelRatio = 1.0;
    addTearDown(tester.view.reset);
    authBloc = AuthBloc(authRepo, coordinator);
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
    when(() => repository.securityEvents())
        .thenAnswer((_) async => <SecurityEventDto>[]);
    when(() => repository.sessions()).thenAnswer((_) async => <SessionDto>[]);
  }

  testWidgets(
      'renders the read-only devices and sessions with backend-authoritative '
      'state and never internal identifiers', (tester) async {
    stubDefaults();
    when(() => repository.loadAccount()).thenAnswer(
      (_) async => _account(devices: [
        _device(),
        _device(id: 'd2', name: 'Phone', isTrusted: false),
      ]),
    );
    when(() => repository.sessions()).thenAnswer((_) async => [
          _session(
              deviceName: 'Back office terminal', devicePlatform: 'Windows'),
          _session(
            id: 's2',
            deviceId: 'd2',
            deviceName: 'Personal',
            isActive: false,
          ),
        ]);

    await pumpHarness(tester);

    expect(find.text('Identity & Security'), findsOneWidget);
    expect(find.text('Devices'), findsOneWidget);
    expect(find.text('Back office terminal'), findsOneWidget); // device row
    expect(find.text('Trusted'), findsOneWidget);
    expect(find.text('Not trusted'), findsOneWidget);
    expect(find.text('Active sessions'), findsOneWidget);
    expect(find.text('Active'), findsNWidgets(2)); // overview + session row
    expect(find.text('Inactive'), findsOneWidget);

    // Session rows render backend-supplied device metadata only:
    // "name · platform" when present, bare name when platform is absent.
    expect(find.text('Back office terminal · Windows'), findsOneWidget);
    expect(find.text('Personal'), findsOneWidget);

    // Backend fields are authoritative; invented details are never rendered.
    expect(find.textContaining('IP'), findsNothing);
    expect(find.textContaining('User-Agent'), findsNothing);
    expect(find.textContaining('· last seen'), findsNothing);
    expect(find.textContaining('Unknown device'), findsNothing);
    // Internal identifiers must never leak into the UI.
    expect(find.text('u1'), findsNothing);
    expect(find.text('d1'), findsNothing);
    expect(find.text('d2'), findsNothing);
    expect(find.text('s1'), findsNothing);
    expect(find.text('s2'), findsNothing);
    expect(find.text('m1'), findsNothing);

    await tester.pumpWidget(const SizedBox());
    await tester.pump(const Duration(milliseconds: 1));
  });

  testWidgets('a disabled account offers MFA set-up and completes enrollment',
      (tester) async {
    stubDefaults();
    when(() => repository.loadAccount()).thenAnswer((_) async => _account());
    when(() => repository.beginMfaEnrollment())
        .thenAnswer((_) async => _enrollment());
    when(() =>
            repository.completeMfaEnrollment(mfaMethodId: 'm1', code: '123456'))
        .thenAnswer((_) async {});

    await pumpHarness(tester);

    expect(find.text('Security code (MFA): Disabled'), findsOneWidget);
    expect(find.byKey(const Key('mfa-setup')), findsOneWidget);

    await tester.tap(find.byKey(const Key('mfa-setup')));
    await pumpFrames(tester);

    // The one-time material is visible for the authenticator app.
    expect(find.text('BASE32SECRET'), findsOneWidget);
    expect(
        find.textContaining('otpauth://totp/CommunityOS:ada'), findsOneWidget);
    expect(find.byKey(const Key('mfa-setup')), findsNothing);
    verify(() => repository.beginMfaEnrollment()).called(1);

    await tester.enterText(find.byKey(const Key('mfa-enroll-code')), '123456');
    await tester.tap(find.byKey(const Key('mfa-enroll-verify')));
    await pumpFrames(tester);

    verify(() =>
            repository.completeMfaEnrollment(mfaMethodId: 'm1', code: '123456'))
        .called(1);
    // The one-time material is cleared and replaced by a success state.
    expect(
      find.text(
          'Security codes are now enabled. A 6-digit verification code will '
          'be required at sign in.'),
      findsOneWidget,
    );
    expect(find.text('BASE32SECRET'), findsNothing);
    expect(find.textContaining('otpauth://'), findsNothing);
    // Success refreshes the authoritative account overview (/me).
    verify(() => repository.loadAccount()).called(2);

    await tester.pumpWidget(const SizedBox());
    await tester.pump(const Duration(milliseconds: 1));
  });

  testWidgets('cancelling enrollment abandons it and clears one-time material',
      (tester) async {
    stubDefaults();
    when(() => repository.loadAccount()).thenAnswer((_) async => _account());
    when(() => repository.beginMfaEnrollment())
        .thenAnswer((_) async => _enrollment());

    await pumpHarness(tester);

    await tester.tap(find.byKey(const Key('mfa-setup')));
    await pumpFrames(tester);
    expect(find.text('BASE32SECRET'), findsOneWidget);

    await tester.tap(find.widgetWithText(OutlinedButton, 'Cancel'));
    await pumpFrames(tester);

    expect(find.byKey(const Key('mfa-setup')), findsOneWidget);
    expect(find.text('BASE32SECRET'), findsNothing);
    expect(find.textContaining('otpauth://'), findsNothing);
    verifyNever(() => repository.completeMfaEnrollment(
        mfaMethodId: any(named: 'mfaMethodId'), code: any(named: 'code')));

    await tester.pumpWidget(const SizedBox());
    await tester.pump(const Duration(milliseconds: 1));
  });

  testWidgets('a sessions read failure shows a retry, not an empty list',
      (tester) async {
    stubDefaults();
    when(() => repository.loadAccount()).thenAnswer((_) async => _account());
    when(() => repository.sessions())
        .thenThrow(const NetworkException('offline'));

    await pumpHarness(tester);

    expect(find.text('offline'), findsOneWidget);
    expect(find.text('Try again'), findsOneWidget);
    expect(find.text('No active session records.'), findsNothing);

    when(() => repository.sessions()).thenAnswer((_) async => [_session()]);
    await tester.tap(find.text('Try again'));
    await pumpFrames(tester);

    expect(find.text('Active'), findsNWidgets(2));
    expect(find.text('offline'), findsNothing);
    verify(() => repository.sessions()).called(2);

    await tester.pumpWidget(const SizedBox());
    await tester.pump(const Duration(milliseconds: 1));
  });

  testWidgets(
      'a completion failure is localized and never rendered as disabled',
      (tester) async {
    stubDefaults();
    when(() => repository.loadAccount()).thenAnswer((_) async => _account());
    when(() => repository.beginMfaEnrollment())
        .thenAnswer((_) async => _enrollment());
    when(() =>
            repository.completeMfaEnrollment(mfaMethodId: 'm1', code: '654321'))
        .thenThrow(const UnauthorizedException(
      'bad code',
      messageKey: 'mfa_invalidCode',
      statusCode: 400,
    ));

    await pumpHarness(tester);
    await tester.tap(find.byKey(const Key('mfa-setup')));
    await pumpFrames(tester);

    await tester.enterText(find.byKey(const Key('mfa-enroll-code')), '654321');
    await tester.tap(find.byKey(const Key('mfa-enroll-verify')));
    await pumpFrames(tester);

    expect(find.text('The verification code is invalid. Please try again.'),
        findsOneWidget);
    expect(find.byKey(const Key('mfa-setup')), findsNothing);
    expect(find.textContaining('not enabled'), findsNothing);
    expect(find.text('BASE32SECRET'), findsNothing);
    // Still on the account page — failure never tears the session down.
    expect(find.byType(AccountPage), findsOneWidget);

    await tester.pumpWidget(const SizedBox());
    await tester.pump(const Duration(milliseconds: 1));
  });

  testWidgets('the enrollment code is validated to exactly six digits',
      (tester) async {
    stubDefaults();
    when(() => repository.loadAccount()).thenAnswer((_) async => _account());
    when(() => repository.beginMfaEnrollment())
        .thenAnswer((_) async => _enrollment());

    await pumpHarness(tester);
    await tester.tap(find.byKey(const Key('mfa-setup')));
    await pumpFrames(tester);

    await tester.enterText(find.byKey(const Key('mfa-enroll-code')), '12345');
    await tester.tap(find.byKey(const Key('mfa-enroll-verify')));
    await pumpFrames(tester);

    expect(find.text('Enter exactly 6 digits.'), findsOneWidget);
    verifyNever(() => repository.completeMfaEnrollment(
        mfaMethodId: any(named: 'mfaMethodId'), code: any(named: 'code')));

    await tester.pumpWidget(const SizedBox());
    await tester.pump(const Duration(milliseconds: 1));
  });

  testWidgets('the security section scales to large text without overflow',
      (tester) async {
    stubDefaults();
    when(() => repository.loadAccount()).thenAnswer(
      (_) async => _mfaEnabledAccount(),
    );
    when(() => repository.sessions()).thenAnswer(
      (_) async => [
        _session(
          deviceName: 'Back office terminal',
          devicePlatform: 'Windows',
        ),
      ],
    );

    // double scale pushes the old fixed-width row layouts to overflow.
    await pumpHarness(tester, textScale: 2.0);

    expect(tester.takeException(), isNull);
    expect(find.text('Identity & Security'), findsOneWidget);
    expect(find.text('Active sessions'), findsOneWidget);

    await tester.pumpWidget(const SizedBox());
    await tester.pump(const Duration(milliseconds: 1));
  });

  testWidgets(
      'session rows render device metadata as "name · platform", each '
      'component alone, or not at all', (tester) async {
    stubDefaults();
    when(() => repository.loadAccount())
        .thenAnswer((_) async => _account(devices: []));
    when(() => repository.sessions()).thenAnswer((_) async => [
          _session(
            id: 's-name',
            deviceId: 'd1',
            deviceName: 'Phone',
          ),
          _session(
            id: 's-platform',
            deviceId: 'd2',
            devicePlatform: 'Android',
          ),
          _session(
            id: 's-both',
            deviceId: 'd3',
            deviceName: 'Back office terminal',
            devicePlatform: 'Windows',
          ),
          _session(id: 's-neither', deviceId: 'd4'),
        ]);

    await pumpHarness(tester);

    expect(find.text('Phone'), findsOneWidget);
    expect(find.text('Android'), findsOneWidget);
    expect(find.text('Back office terminal · Windows'), findsOneWidget);
    // Contained exactly once inside the combined label, never duplicated.
    expect(find.text('Back office terminal'), findsNothing);
    expect(find.text('Windows'), findsNothing);

    // No label is fabricated for a session without device metadata.
    expect(find.textContaining('Unknown device'), findsNothing);
    expect(find.text('d4'), findsNothing);

    await tester.pumpWidget(const SizedBox());
    await tester.pump(const Duration(milliseconds: 1));
  });

  // ── MFA removal ──────────────────────────────────────────────────────

  testWidgets(
      'an MFA-enabled account exposes the remove button and the confirm '
      'dialog gates the removal', (tester) async {
    stubDefaults();
    when(() => repository.loadAccount())
        .thenAnswer((_) async => _mfaEnabledAccount());
    when(() => repository.removeMfaMethod('m1')).thenAnswer((_) async {});

    await pumpHarness(tester);

    expect(find.text('Security code (MFA): Enabled'), findsOneWidget);
    expect(find.byKey(const Key('mfa-remove')), findsOneWidget);
    // The "Set up" button is not rendered when MFA is already active.
    expect(find.byKey(const Key('mfa-setup')), findsNothing);

    // Cancel closes the dialog and never issues a request.
    await tester.tap(find.byKey(const Key('mfa-remove')));
    await pumpFrames(tester);
    expect(find.text('Remove this security method?'), findsOneWidget);
    await tester.tap(find.text('Cancel'));
    await pumpFrames(tester);
    expect(find.text('Remove this security method?'), findsNothing);
    verifyNever(() => repository.removeMfaMethod(any()));

    // Confirming triggers removal; success triggers session expiry.
    await tester.tap(find.byKey(const Key('mfa-remove')));
    await pumpFrames(tester);
    await tester.tap(find.byKey(const Key('mfa-remove-confirm')));
    await pumpFrames(tester);

    verify(() => repository.removeMfaMethod('m1')).called(1);
    // Session expiry is issued because the backend revoked every session.
    verify(() => authRepo.clearLocalAuth()).called(1);

    await tester.pumpWidget(const SizedBox());
    await tester.pump(const Duration(milliseconds: 1));
  });

  testWidgets(
      'while a removal is in flight the section shows in-progress indicator '
      'and suppresses a second tap', (tester) async {
    stubDefaults();
    when(() => repository.loadAccount())
        .thenAnswer((_) async => _mfaEnabledAccount());
    final gate = Completer<void>();
    when(() => repository.removeMfaMethod('m1')).thenAnswer((_) => gate.future);

    await pumpHarness(tester);

    await tester.tap(find.byKey(const Key('mfa-remove')));
    await pumpFrames(tester);
    await tester.tap(find.byKey(const Key('mfa-remove-confirm')));
    await tester.pump(const Duration(milliseconds: 100));

    expect(find.byKey(const Key('mfa-remove')), findsNothing);
    expect(find.byType(CircularProgressIndicator), findsOneWidget);

    gate.complete();
    await pumpFrames(tester);

    verify(() => repository.removeMfaMethod('m1')).called(1);
    verify(() => authRepo.clearLocalAuth()).called(1);

    await tester.pumpWidget(const SizedBox());
    await tester.pump(const Duration(milliseconds: 1));
  });

  testWidgets(
      'a failed removal shows a retryable error and retry re-dispatches',
      (tester) async {
    stubDefaults();
    when(() => repository.loadAccount())
        .thenAnswer((_) async => _mfaEnabledAccount());
    var attempts = 0;
    when(() => repository.removeMfaMethod('m1')).thenAnswer((_) async {
      attempts++;
      if (attempts == 1) {
        throw const ValidationException('final mfa', statusCode: 409);
      }
    });

    await pumpHarness(tester);

    await tester.tap(find.byKey(const Key('mfa-remove')));
    await pumpFrames(tester);
    await tester.tap(find.byKey(const Key('mfa-remove-confirm')));
    await pumpFrames(tester);

    expect(find.text('This security method could not be removed.'),
        findsOneWidget);
    expect(find.byKey(const Key('mfa-remove-retry-m1')), findsOneWidget);
    expect(find.byKey(const Key('mfa-remove')), findsNothing);

    await tester.tap(find.byKey(const Key('mfa-remove-retry-m1')));
    await pumpFrames(tester);

    // Retry re-dispatches directly: no confirm dialog, no duplicate dispatch.
    expect(find.text('Remove this security method?'), findsNothing);
    expect(find.byType(CircularProgressIndicator), findsNothing);
    expect(find.byKey(const Key('mfa-remove-retry-m1')), findsNothing);
    verify(() => repository.removeMfaMethod('m1')).called(2);

    await tester.pumpWidget(const SizedBox());
    await tester.pump(const Duration(milliseconds: 1));
  });

  testWidgets(
      'cancelling a failed removal returns to the idle remove control '
      'without re-dispatching', (tester) async {
    stubDefaults();
    when(() => repository.loadAccount())
        .thenAnswer((_) async => _mfaEnabledAccount());
    when(() => repository.removeMfaMethod('m1')).thenThrow(
      const ValidationException('final mfa', statusCode: 409),
    );

    await pumpHarness(tester);

    await tester.tap(find.byKey(const Key('mfa-remove')));
    await pumpFrames(tester);
    await tester.tap(find.byKey(const Key('mfa-remove-confirm')));
    await pumpFrames(tester);

    // The failure view carries the stable localized message and a cancel.
    expect(find.text('This security method could not be removed.'),
        findsOneWidget);
    expect(find.byKey(const Key('mfa-remove-cancel')), findsOneWidget);

    await tester.tap(find.byKey(const Key('mfa-remove-cancel')));
    await pumpFrames(tester);

    // Back to the idle remove control; no second request is dispatched.
    expect(find.byKey(const Key('mfa-remove')), findsOneWidget);
    expect(find.byKey(const Key('mfa-remove-cancel')), findsNothing);
    verify(() => repository.removeMfaMethod('m1')).called(1);

    await tester.pumpWidget(const SizedBox());
    await tester.pump(const Duration(milliseconds: 1));
  });

  testWidgets(
      'an MFA-disabled account still shows the set-up button, not the '
      'remove button', (tester) async {
    stubDefaults();
    when(() => repository.loadAccount()).thenAnswer((_) async => _account());

    await pumpHarness(tester);

    expect(find.byKey(const Key('mfa-setup')), findsOneWidget);
    expect(find.byKey(const Key('mfa-remove')), findsNothing);

    await tester.pumpWidget(const SizedBox());
    await tester.pump(const Duration(milliseconds: 1));
  });

  testWidgets('the remove control scales to large text without overflow',
      (tester) async {
    stubDefaults();
    when(() => repository.loadAccount())
        .thenAnswer((_) async => _mfaEnabledAccount());

    await pumpHarness(tester, textScale: 2.0);

    expect(tester.takeException(), isNull);
    expect(find.byKey(const Key('mfa-remove')), findsOneWidget);

    await tester.pumpWidget(const SizedBox());
    await tester.pump(const Duration(milliseconds: 1));
  });
}
