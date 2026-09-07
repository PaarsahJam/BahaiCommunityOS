import 'dart:async';

import 'package:flutter_test/flutter_test.dart';
import 'package:member_portal/core/error/app_exception.dart';
import 'package:member_portal/features/account/application/account_bloc.dart';
import 'package:member_portal/features/account/application/account_event.dart';
import 'package:member_portal/features/account/application/account_state.dart';
import 'package:member_portal/features/account/domain/account_exceptions.dart';
import 'package:member_portal/features/auth/data/auth_dtos.dart';
import 'package:member_portal/features/member/domain/member_repository.dart';
import 'package:mocktail/mocktail.dart';

class _MockMemberRepository extends Mock implements MemberRepository {}

Future<void> _flush() async {
  await pumpEventQueue();
  await Future<void>.delayed(Duration.zero);
  await pumpEventQueue();
}

UserAccountDto _account() => UserAccountDto(
      id: 'u1',
      email: 'ada@example.org',
      status: 'Active',
      createdOn: DateTime(2020, 1, 1),
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
      description: description,
      occurredOn: occurredOn ?? DateTime(2026, 9, 7, 12),
    );

void main() {
  late _MockMemberRepository repository;

  setUp(() {
    repository = _MockMemberRepository();
  });

  group('AccountBloc', () {
    test('loads the overview and the recent security activity', () async {
      when(() => repository.loadAccount()).thenAnswer((_) async => _account());
      when(() => repository.securityEvents()).thenAnswer((_) async => [
            _event('Login.Succeeded'),
            _event('Password.Changed', occurredOn: DateTime(2026, 9, 6)),
          ]);

      final bloc = AccountBloc(repository);
      addTearDown(bloc.close);
      final states = <AccountState>[];
      final sub = bloc.stream.listen(states.add);
      bloc.add(const AccountEvent.requested());
      await _flush();

      expect(states, contains(isA<AccountLoading>()));
      final loaded = states.last as AccountLoaded;
      expect(loaded.account.id, 'u1');
      expect(loaded.account.email, 'ada@example.org');
      expect(loaded.securityEvents, hasLength(2));
      expect(loaded.securityEventsError, isNull);
      expect(loaded.passwordStatus, isA<PasswordIdle>());
      await sub.cancel();
    });

    test('a 403 overview failure maps to a keyed account-forbidden error',
        () async {
      when(() => repository.loadAccount())
          .thenThrow(const ForbiddenException('nope'));

      final bloc = AccountBloc(repository);
      addTearDown(bloc.close);
      final states = <AccountState>[];
      final sub = bloc.stream.listen(states.add);
      bloc.add(const AccountEvent.requested());
      await _flush();

      expect(states.last, isA<AccountFailed>());
      final error = (states.last as AccountFailed).error;
      expect(error, isA<ForbiddenException>());
      expect(error.messageKey, 'account_forbidden');
      expect(states.whereType<AccountLoaded>(), isEmpty);
      await sub.cancel();
    });

    test('a 404 overview failure maps to a keyed account-not-found error',
        () async {
      when(() => repository.loadAccount())
          .thenThrow(const NotFoundException('missing'));

      final bloc = AccountBloc(repository);
      addTearDown(bloc.close);
      final states = <AccountState>[];
      final sub = bloc.stream.listen(states.add);
      bloc.add(const AccountEvent.requested());
      await _flush();

      expect(states.last, isA<AccountFailed>());
      final error = (states.last as AccountFailed).error;
      expect(error, isA<NotFoundException>());
      expect(error.messageKey, 'account_notFound');
      await sub.cancel();
    });

    test('an overview network failure stays retryable and reloads', () async {
      when(() => repository.loadAccount())
          .thenThrow(const NetworkException('offline'));

      final bloc = AccountBloc(repository);
      addTearDown(bloc.close);
      final states = <AccountState>[];
      final sub = bloc.stream.listen(states.add);
      bloc.add(const AccountEvent.requested());
      await _flush();
      expect(states.last, isA<AccountFailed>());

      when(() => repository.loadAccount()).thenAnswer((_) async => _account());
      when(() => repository.securityEvents())
          .thenAnswer((_) async => <SecurityEventDto>[]);
      bloc.add(const AccountEvent.retryRequested());
      await _flush();

      expect(states.last, isA<AccountLoaded>());
      expect((states.last as AccountLoaded).account.id, 'u1');
      verify(() => repository.loadAccount()).called(2);
      await sub.cancel();
    });

    test(
        'a security-activity failure is non-fatal and exposes a retry that '
        'reloads only the section', () async {
      when(() => repository.loadAccount()).thenAnswer((_) async => _account());
      when(() => repository.securityEvents())
          .thenThrow(const NetworkException('offline'));

      final bloc = AccountBloc(repository);
      addTearDown(bloc.close);
      final states = <AccountState>[];
      final sub = bloc.stream.listen(states.add);
      bloc.add(const AccountEvent.requested());
      await _flush();

      final failed = states.last as AccountLoaded;
      expect(failed.account.id, 'u1');
      expect(failed.securityEventsError, isA<NetworkException>());
      expect(failed.securityEvents, isEmpty);

      when(() => repository.securityEvents())
          .thenAnswer((_) async => [_event('Login.Succeeded')]);
      bloc.add(const AccountEvent.securityEventsRequested());
      await _flush();

      final reloaded = states.last as AccountLoaded;
      expect(reloaded.securityEventsError, isNull);
      expect(reloaded.securityEvents, hasLength(1));
      verify(() => repository.loadAccount()).called(1);
      await sub.cancel();
    });

    test('a password change succeeds and reports the success status', () async {
      when(() => repository.loadAccount()).thenAnswer((_) async => _account());
      when(() => repository.securityEvents())
          .thenAnswer((_) async => <SecurityEventDto>[]);
      when(() => repository.changePassword(
          currentPassword: any(named: 'currentPassword'),
          newPassword: any(named: 'newPassword'))).thenAnswer((_) async {});

      final bloc = AccountBloc(repository);
      addTearDown(bloc.close);
      final states = <AccountState>[];
      final sub = bloc.stream.listen(states.add);
      bloc.add(const AccountEvent.requested());
      await _flush();

      bloc.add(const AccountEvent.passwordChangeRequested(
        currentPassword: 'old-pass',
        newPassword: 'new-pass',
      ));
      await _flush();

      final status = (states.last as AccountLoaded).passwordStatus;
      expect(status, isA<PasswordSucceeded>());
      expect(
          states,
          contains(isA<AccountLoaded>().having(
              (s) => s.passwordStatus, 'status', isA<PasswordSubmitting>())));
      verify(() => repository.changePassword(
            currentPassword: 'old-pass',
            newPassword: 'new-pass',
          )).called(1);
      await sub.cancel();
    });

    test('a wrong current password stays a form-level failure', () async {
      when(() => repository.loadAccount()).thenAnswer((_) async => _account());
      when(() => repository.securityEvents())
          .thenAnswer((_) async => <SecurityEventDto>[]);
      when(() => repository.changePassword(
              currentPassword: any(named: 'currentPassword'),
              newPassword: any(named: 'newPassword')))
          .thenThrow(const UnauthorizedException(
        'The current password is incorrect.',
        messageKey: 'accountCurrentPasswordIncorrect',
        statusCode: 401,
      ));

      final bloc = AccountBloc(repository);
      addTearDown(bloc.close);
      final states = <AccountState>[];
      final sub = bloc.stream.listen(states.add);
      bloc.add(const AccountEvent.requested());
      await _flush();

      bloc.add(const AccountEvent.passwordChangeRequested(
        currentPassword: 'wrong',
        newPassword: 'new-pass',
      ));
      await _flush();

      final failed = (states.last as AccountLoaded).passwordStatus;
      expect(failed, isA<PasswordFailed>());
      final error = (failed as PasswordFailed).error;
      expect(error, isA<UnauthorizedException>());
      expect(error!.messageKey, 'accountCurrentPasswordIncorrect');
      // The overview and security section stay on screen.
      expect((states.last as AccountLoaded).account.id, 'u1');
      await sub.cancel();
    });

    test('a validator 400 surfaces server field errors on the form', () async {
      when(() => repository.loadAccount()).thenAnswer((_) async => _account());
      when(() => repository.securityEvents())
          .thenAnswer((_) async => <SecurityEventDto>[]);
      when(() => repository.changePassword(
              currentPassword: any(named: 'currentPassword'),
              newPassword: any(named: 'newPassword')))
          .thenThrow(const PasswordValidationException(
        'password policy',
        fieldErrors: {'newpassword': 'must be at least 12 characters'},
        messageKey: 'accountPasswordValidationFailed',
        statusCode: 400,
      ));

      final bloc = AccountBloc(repository);
      addTearDown(bloc.close);
      final states = <AccountState>[];
      final sub = bloc.stream.listen(states.add);
      bloc.add(const AccountEvent.requested());
      await _flush();

      bloc.add(const AccountEvent.passwordChangeRequested(
        currentPassword: 'old-pass',
        newPassword: 'short',
      ));
      await _flush();

      final failed = (states.last as AccountLoaded).passwordStatus;
      expect(failed, isA<PasswordFailed>());
      final error = (failed as PasswordFailed).error;
      expect(error, isA<PasswordValidationException>());
      expect((error! as PasswordValidationException).fieldErrors,
          contains('newpassword'));
      await sub.cancel();
    });

    test('a second submit while one is in flight is ignored', () async {
      when(() => repository.loadAccount()).thenAnswer((_) async => _account());
      when(() => repository.securityEvents())
          .thenAnswer((_) async => <SecurityEventDto>[]);
      final gate = Completer<void>();
      when(() => repository.changePassword(
              currentPassword: any(named: 'currentPassword'),
              newPassword: any(named: 'newPassword')))
          .thenAnswer((_) => gate.future);

      final bloc = AccountBloc(repository);
      addTearDown(bloc.close);
      bloc.add(const AccountEvent.requested());
      await _flush();

      bloc.add(const AccountEvent.passwordChangeRequested(
        currentPassword: 'old-pass',
        newPassword: 'new-pass',
      ));
      bloc.add(const AccountEvent.passwordChangeRequested(
        currentPassword: 'old-pass',
        newPassword: 'new-pass',
      ));
      await pumpEventQueue();

      expect(bloc.state, isA<AccountLoaded>());
      expect((bloc.state as AccountLoaded).passwordStatus,
          isA<PasswordSubmitting>());
      verify(() => repository.changePassword(
            currentPassword: any(named: 'currentPassword'),
            newPassword: any(named: 'newPassword'),
          )).called(1);

      gate.complete();
      await _flush();
      await bloc.close();
    });

    test('a closed bloc never emits after an in-flight load completes',
        () async {
      final gate = Completer<UserAccountDto>();
      when(() => repository.loadAccount()).thenAnswer((_) => gate.future);

      final bloc = AccountBloc(repository);
      bloc.add(const AccountEvent.requested());
      await pumpEventQueue();

      await bloc.close();

      gate.complete(_account());
      await _flush();

      expect(bloc.isClosed, isTrue);
    });
  });
}
