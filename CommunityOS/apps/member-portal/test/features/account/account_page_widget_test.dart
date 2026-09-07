import 'dart:async';

import 'package:flutter/material.dart';
import 'package:flutter_bloc/flutter_bloc.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:member_portal/core/error/app_exception.dart';
import 'package:member_portal/core/network/refresh_coordinator.dart';
import 'package:member_portal/features/account/application/account_bloc.dart';
import 'package:member_portal/features/account/domain/account_exceptions.dart';
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

UserAccountDto _account({List<MfaMethodDto> mfaMethods = const []}) =>
    UserAccountDto(
      id: 'u1',
      email: 'ada@example.org',
      status: 'Active',
      createdOn: DateTime(2020, 1, 1),
      mfaMethods: mfaMethods,
    );

UserAccountDto _accountWithMfa() => _account(
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

SecurityEventDto _event(
  String eventType, {
  DateTime? occurredOn,
  String? description,
}) =>
    SecurityEventDto(
      id: 'e1',
      eventType: eventType,
      description: description ?? 'legacy free-text detail',
      occurredOn: occurredOn ?? DateTime(2026, 9, 7, 12),
    );

String widgetText(Widget widget) => (widget as Text).data ?? '';

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

  Future<void> pumpAccountHarness(WidgetTester tester) async {
    // The whole page (overview + password form + security activity) must be
    // laid out so the password controls are buildable and tappable. ListView
    // builds lazily, so give the test surface a tall viewport.
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
        home: BlocProvider<AuthBloc>.value(
          value: authBloc,
          child: AccountPage(
            createAccountBloc: () => AccountBloc(repository),
          ),
        ),
      ),
    );
    await pumpFrames(tester);
  }

  Future<void> pumpAccount(
    WidgetTester tester, {
    List<SecurityEventDto> events = const [],
  }) async {
    when(() => repository.loadAccount()).thenAnswer((_) async => _account());
    when(() => repository.securityEvents()).thenAnswer((_) async => events);
    await pumpAccountHarness(tester);
  }

  const currentKey = Key('current-password');
  const newKey = Key('new-password');
  const confirmKey = Key('confirm-password');

  Future<void> fillPasswordForm(
    WidgetTester tester, {
    String current = 'old-pass',
    String newPassword = 'new-pass',
    String confirm = 'new-pass',
  }) async {
    await tester.enterText(find.byKey(currentKey), current);
    await tester.enterText(find.byKey(newKey), newPassword);
    await tester.enterText(find.byKey(confirmKey), confirm);
  }

  Future<void> submitPassword(WidgetTester tester) async {
    await tester.tap(find.widgetWithText(FilledButton, 'Change password'));
    await pumpFrames(tester);
  }

  testWidgets('shows a loading state while the overview is resolving',
      (tester) async {
    final gate = Completer<UserAccountDto>();
    when(() => repository.loadAccount()).thenAnswer((_) => gate.future);
    when(() => repository.securityEvents())
        .thenAnswer((_) async => <SecurityEventDto>[]);

    await pumpAccountHarness(tester);
    await tester.pump(const Duration(milliseconds: 100));

    expect(find.byType(CircularProgressIndicator), findsOneWidget);

    gate.complete(_account());
    await pumpFrames(tester);

    expect(find.byType(CircularProgressIndicator), findsNothing);
    expect(find.text('ada@example.org'), findsOneWidget);
    verify(() => repository.loadAccount()).called(1);
    verify(() => repository.securityEvents()).called(1);

    await tester.pumpWidget(const SizedBox());
    await tester.pump(const Duration(milliseconds: 1));
  });

  testWidgets('renders the overview without exposing internal identifiers',
      (tester) async {
    await pumpAccount(tester, events: [_event('Login.Succeeded')]);

    expect(find.text('ada@example.org'), findsOneWidget);
    expect(find.text('Active'), findsOneWidget);
    expect(find.text('Jan 1, 2020'), findsOneWidget);
    expect(find.text('Security code (MFA): Disabled'), findsOneWidget);
    // Internal account/event ids must never be rendered.
    expect(find.text('u1'), findsNothing);
    expect(find.text('e1'), findsNothing);

    await tester.pumpWidget(const SizedBox());
    await tester.pump(const Duration(milliseconds: 1));
  });

  testWidgets('renders the MFA-enabled chip when the backend reports a method',
      (tester) async {
    when(() => repository.loadAccount())
        .thenAnswer((_) async => _accountWithMfa());
    when(() => repository.securityEvents())
        .thenAnswer((_) async => <SecurityEventDto>[]);

    await pumpAccountHarness(tester);

    expect(find.text('Security code (MFA): Enabled'), findsOneWidget);

    await tester.pumpWidget(const SizedBox());
    await tester.pump(const Duration(milliseconds: 1));
  });

  testWidgets(
      'renders security activity newest-first with stable localized labels '
      'and never the free-text description', (tester) async {
    await pumpAccount(tester, events: [
      _event(
        'RefreshToken.ReuseDetected',
        occurredOn: DateTime(2026, 9, 6, 9),
      ),
      _event('Login.Succeeded', occurredOn: DateTime(2026, 9, 7, 12)),
    ]);

    expect(find.text('Recent security activity'), findsOneWidget);
    // Labels are keyed by EventType, not driven by free-text Description.
    expect(find.text('Signed in'), findsOneWidget);
    expect(find.text('Session suspicious activity detected'), findsOneWidget);
    expect(find.text('legacy free-text detail'), findsNothing);
    // Newest first: the login event (12:00) is rendered before the reuse
    // event (09:00 two days earlier).
    final tiles = tester
        .widgetList<ListTile>(find.byType(ListTile))
        .map((tile) => widgetText(tile.title!))
        .toList();
    expect(tiles,
        orderedEquals(['Signed in', 'Session suspicious activity detected']));

    await tester.pumpWidget(const SizedBox());
    await tester.pump(const Duration(milliseconds: 1));
  });

  testWidgets('renders an empty security-activity state on a 200 + empty list',
      (tester) async {
    await pumpAccount(tester);

    expect(find.text('No recent security activity.'), findsOneWidget);
    expect(find.byType(CircularProgressIndicator), findsNothing);

    await tester.pumpWidget(const SizedBox());
    await tester.pump(const Duration(milliseconds: 1));
  });

  testWidgets('a security-activity failure is retryable and keeps the overview',
      (tester) async {
    when(() => repository.loadAccount()).thenAnswer((_) async => _account());
    when(() => repository.securityEvents())
        .thenThrow(const NetworkException('offline'));

    await pumpAccountHarness(tester);

    expect(find.text('offline'), findsOneWidget);
    expect(find.text('Try again'), findsOneWidget);
    expect(find.text('ada@example.org'), findsOneWidget);

    when(() => repository.securityEvents())
        .thenAnswer((_) async => [_event('Login.Succeeded')]);
    await tester.tap(find.text('Try again'));
    await pumpFrames(tester);

    expect(find.text('Signed in'), findsOneWidget);
    expect(find.text('offline'), findsNothing);
    verify(() => repository.securityEvents()).called(2);

    await tester.pumpWidget(const SizedBox());
    await tester.pump(const Duration(milliseconds: 1));
  });

  testWidgets('a 403 overview renders the localized forbidden state',
      (tester) async {
    when(() => repository.loadAccount())
        .thenThrow(const ForbiddenException('nope'));
    when(() => repository.securityEvents())
        .thenAnswer((_) async => <SecurityEventDto>[]);

    await pumpAccountHarness(tester);

    expect(
      find.text('Your account information cannot be viewed at this time.'),
      findsOneWidget,
    );
    expect(find.text('Try again'), findsOneWidget);
    expect(find.text('ada@example.org'), findsNothing);
    expect(find.byType(CircularProgressIndicator), findsNothing);

    await tester.pumpWidget(const SizedBox());
    await tester.pump(const Duration(milliseconds: 1));
  });

  testWidgets('a 404 overview renders the localized not-found state',
      (tester) async {
    when(() => repository.loadAccount())
        .thenThrow(const NotFoundException('missing'));
    when(() => repository.securityEvents())
        .thenAnswer((_) async => <SecurityEventDto>[]);

    await pumpAccountHarness(tester);

    expect(find.text('Your account information could not be found.'),
        findsOneWidget);
    expect(find.text('Try again'), findsOneWidget);

    await tester.pumpWidget(const SizedBox());
    await tester.pump(const Duration(milliseconds: 1));
  });

  testWidgets('client-side validation surfaces required and mismatch messages',
      (tester) async {
    await pumpAccount(tester);

    await submitPassword(tester);

    expect(find.text('Enter your current password'), findsOneWidget);
    expect(find.text('Enter a new password'), findsOneWidget);
    expect(find.text('Confirm your new password'), findsOneWidget);

    await fillPasswordForm(
      tester,
      current: 'old-pass',
      newPassword: 'new-pass',
      confirm: 'different',
    );
    await submitPassword(tester);

    expect(find.text('The new passwords do not match'), findsOneWidget);
    verifyNever(() => repository.changePassword(
        currentPassword: any(named: 'currentPassword'),
        newPassword: any(named: 'newPassword')));

    await tester.pumpWidget(const SizedBox());
    await tester.pump(const Duration(milliseconds: 1));
  });

  testWidgets(
      'a wrong current password shows a form-level error and never tears the '
      'session down', (tester) async {
    await pumpAccount(tester);
    when(() => repository.changePassword(
        currentPassword: any(named: 'currentPassword'),
        newPassword: any(named: 'newPassword'))).thenThrow(
      const UnauthorizedException(
        'The current password is incorrect.',
        messageKey: 'accountCurrentPasswordIncorrect',
        statusCode: 401,
      ),
    );

    await fillPasswordForm(tester, current: 'wrong');
    await submitPassword(tester);

    expect(find.text('Your current password is incorrect.'), findsOneWidget);
    expect(find.byType(AccountPage), findsOneWidget);
    verifyNever(() => authRepo.clearLocalAuth());
    verify(() => repository.changePassword(
        currentPassword: 'wrong', newPassword: 'new-pass')).called(1);

    await tester.pumpWidget(const SizedBox());
    await tester.pump(const Duration(milliseconds: 1));
  });

  testWidgets(
      'a stale-token 401 surfaces the retryable message without a second '
      'automatic submit', (tester) async {
    await pumpAccount(tester);
    when(() => repository.changePassword(
        currentPassword: any(named: 'currentPassword'),
        newPassword: any(named: 'newPassword'))).thenThrow(
      const UnauthorizedException(
        'Your session is being refreshed.',
        messageKey: 'accountPasswordRetryable',
        statusCode: 401,
      ),
    );

    await fillPasswordForm(tester);
    await submitPassword(tester);

    expect(
      find.text(
          'Your session could not be verified right now. Please try again.'),
      findsOneWidget,
    );
    expect(find.byType(AccountPage), findsOneWidget);
    verifyNever(() => authRepo.clearLocalAuth());
    verify(() => repository.changePassword(
        currentPassword: 'old-pass', newPassword: 'new-pass')).called(1);

    await tester.pumpWidget(const SizedBox());
    await tester.pump(const Duration(milliseconds: 1));
  });

  testWidgets(
      'a validator 400 renders server field errors next to the offending '
      'field', (tester) async {
    await pumpAccount(tester);
    when(() => repository.changePassword(
        currentPassword: any(named: 'currentPassword'),
        newPassword: any(named: 'newPassword'))).thenThrow(
      const PasswordValidationException(
        'password policy',
        fieldErrors: {'newpassword': 'must be at least 12 characters'},
        messageKey: 'accountPasswordValidationFailed',
        statusCode: 400,
      ),
    );

    await fillPasswordForm(tester);
    await submitPassword(tester);

    expect(find.text('must be at least 12 characters'), findsOneWidget);
    expect(find.text('The new password does not meet the requirements.'),
        findsOneWidget);
    expect(find.byType(AccountPage), findsOneWidget);
    verifyNever(() => authRepo.clearLocalAuth());

    await tester.pumpWidget(const SizedBox());
    await tester.pump(const Duration(milliseconds: 1));
  });

  testWidgets('a duplicate submit while one is in flight is ignored',
      (tester) async {
    await pumpAccount(tester);
    final gate = Completer<void>();
    when(() => repository.changePassword(
        currentPassword: any(named: 'currentPassword'),
        newPassword: any(named: 'newPassword'))).thenAnswer((_) => gate.future);

    await fillPasswordForm(tester);
    await tester.tap(find.byType(FilledButton));
    await tester.pump(const Duration(milliseconds: 50));

    // While the mutation is in flight the submit button is disabled, so a
    // duplicate tap cannot fire a second request.
    final button = tester.widget<FilledButton>(find.byType(FilledButton));
    expect(button.onPressed, isNull);
    await tester.tap(find.byType(FilledButton), warnIfMissed: false);
    await tester.pump(const Duration(milliseconds: 50));

    verify(() => repository.changePassword(
        currentPassword: 'old-pass', newPassword: 'new-pass')).called(1);

    gate.complete();
    await pumpFrames(tester);

    await tester.pumpWidget(const SizedBox());
    await tester.pump(const Duration(milliseconds: 1));
  });

  testWidgets(
      'a successful password change hands control to the centralized '
      're-authentication path and clears the form', (tester) async {
    await pumpAccount(tester);
    when(() => authRepo.clearLocalAuth()).thenAnswer((_) async {});
    when(() => repository.changePassword(
        currentPassword: any(named: 'currentPassword'),
        newPassword: any(named: 'newPassword'))).thenAnswer((_) async {});

    await fillPasswordForm(tester);
    await submitPassword(tester);

    verify(() => authRepo.clearLocalAuth()).called(1);
    final current =
        tester.widget<TextFormField>(find.byKey(currentKey)).controller!.text;
    final fresh =
        tester.widget<TextFormField>(find.byKey(newKey)).controller!.text;
    final confirm =
        tester.widget<TextFormField>(find.byKey(confirmKey)).controller!.text;
    expect(current, isEmpty);
    expect(fresh, isEmpty);
    expect(confirm, isEmpty);

    await tester.pumpWidget(const SizedBox());
    await tester.pump(const Duration(milliseconds: 1));
  });
}
