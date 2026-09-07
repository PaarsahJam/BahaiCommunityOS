import 'dart:async';

import 'package:flutter_test/flutter_test.dart';
import 'package:member_portal/core/error/app_exception.dart';
import 'package:member_portal/features/account/application/security_bloc.dart';
import 'package:member_portal/features/account/application/security_event.dart';
import 'package:member_portal/features/account/application/security_state.dart';
import 'package:member_portal/features/auth/data/auth_dtos.dart';
import 'package:member_portal/features/member/domain/member_repository.dart';
import 'package:mocktail/mocktail.dart';

class _MockMemberRepository extends Mock implements MemberRepository {}

Future<void> _flush() async {
  await pumpEventQueue();
  await Future<void>.delayed(Duration.zero);
  await pumpEventQueue();
}

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

MfaEnrollmentDto _enrollment({String methodId = 'm1'}) => MfaEnrollmentDto(
      mfaMethodId: methodId,
      secret: 'BASE32SECRET',
      provisioningUri: 'otpauth://totp/CommunityOS:ada?secret=BASE32SECRET',
    );

void main() {
  late _MockMemberRepository repository;

  setUp(() {
    repository = _MockMemberRepository();
  });

  group('SecurityBloc', () {
    test('loads the read-only session list', () async {
      when(() => repository.sessions()).thenAnswer((_) async => [
            _session(),
            _session(id: 's2', deviceId: 'd2', isActive: false),
          ]);

      final bloc = SecurityBloc(repository);
      addTearDown(bloc.close);
      final states = <SecurityState>[];
      final sub = bloc.stream.listen(states.add);
      bloc.add(const SecurityEvent.sessionsRequested());
      await _flush();

      // The bloc's stream emits transitioned states only; the very first frame
      // is the in-flight loading state, not the construction state.
      expect(states.first, isA<SecurityLoaded>());
      expect((states.first as SecurityLoaded).isSessionsLoading, isTrue);
      final loaded = states.last as SecurityLoaded;
      expect(loaded.sessions, hasLength(2));
      expect(loaded.sessions.first.isActive, isTrue);
      expect(loaded.sessions.last.isActive, isFalse);
      expect(loaded.sessionsError, isNull);
      expect(loaded.mfaStatus, isA<MfaEnrollmentIdle>());
      expect(
        states.any((s) => s is SecurityLoaded && s.isSessionsLoading),
        isTrue,
      );
      await sub.cancel();
    });

    test('a read failure exposes a retryable error, never an empty list',
        () async {
      when(() => repository.sessions())
          .thenThrow(const NetworkException('offline'));

      final bloc = SecurityBloc(repository);
      addTearDown(bloc.close);
      final states = <SecurityState>[];
      final sub = bloc.stream.listen(states.add);
      bloc.add(const SecurityEvent.sessionsRequested());
      await _flush();

      final failed = states.last as SecurityLoaded;
      expect(failed.sessionsError, isA<NetworkException>());
      expect(failed.sessions, isEmpty);

      when(() => repository.sessions()).thenAnswer((_) async => [_session()]);
      bloc.add(const SecurityEvent.sessionsRequested());
      await _flush();

      final retried = states.last as SecurityLoaded;
      expect(retried.sessionsError, isNull);
      expect(retried.sessions, hasLength(1));
      verify(() => repository.sessions()).called(2);
      await sub.cancel();
    });

    test('a later read failure keeps the prior list instead of emptying it',
        () async {
      when(() => repository.sessions()).thenAnswer((_) async => [_session()]);

      final bloc = SecurityBloc(repository);
      addTearDown(bloc.close);
      final states = <SecurityState>[];
      final sub = bloc.stream.listen(states.add);
      bloc.add(const SecurityEvent.sessionsRequested());
      await _flush();
      expect((states.last as SecurityLoaded).sessions, hasLength(1));

      when(() => repository.sessions())
          .thenThrow(const ServerException('boom'));
      bloc.add(const SecurityEvent.sessionsRequested());
      await _flush();

      final failed = states.last as SecurityLoaded;
      expect(failed.sessionsError, isA<ServerException>());
      expect(failed.sessions, hasLength(1));
      await sub.cancel();
    });

    test(
        'enrollment transitions through pending material to succeeded and '
        'clears the one-time secret', () async {
      when(() => repository.sessions()).thenAnswer((_) async => <SessionDto>[]);
      when(() => repository.beginMfaEnrollment())
          .thenAnswer((_) async => _enrollment());
      when(() => repository.completeMfaEnrollment(
          mfaMethodId: 'm1', code: '123456')).thenAnswer((_) async {});

      final bloc = SecurityBloc(repository);
      addTearDown(bloc.close);
      final states = <SecurityState>[];
      final sub = bloc.stream.listen(states.add);
      bloc.add(const SecurityEvent.sessionsRequested());
      await _flush();

      bloc.add(const SecurityEvent.mfaEnrollmentRequested());
      await _flush();

      expect(
        states.any(
            (s) => s is SecurityLoaded && s.mfaStatus is MfaEnrollmentStarting),
        isTrue,
      );
      final pending =
          (states.last as SecurityLoaded).mfaStatus as MfaEnrollmentPending;
      expect(pending.secret, 'BASE32SECRET');
      expect(pending.provisioningUri, contains('otpauth://'));

      // A duplicate request while pending must be ignored.
      bloc.add(const SecurityEvent.mfaEnrollmentRequested());
      await _flush();
      verify(() => repository.beginMfaEnrollment()).called(1);

      bloc.add(const SecurityEvent.mfaEnrollmentCompleted(code: '123456'));
      await _flush();

      expect((states.last as SecurityLoaded).mfaStatus,
          isA<MfaEnrollmentSucceeded>());
      expect(
        states.any((s) =>
            s is SecurityLoaded && s.mfaStatus is MfaEnrollmentSubmitting),
        isTrue,
      );
      verify(() => repository.completeMfaEnrollment(
          mfaMethodId: 'm1', code: '123456')).called(1);
      // The one-time material must not survive success.
      expect(states.last.toString().contains('BASE32SECRET'), isFalse);
      await sub.cancel();
    });

    test('cancellation from pending clears the one-time material', () async {
      when(() => repository.sessions()).thenAnswer((_) async => <SessionDto>[]);
      when(() => repository.beginMfaEnrollment())
          .thenAnswer((_) async => _enrollment());

      final bloc = SecurityBloc(repository);
      addTearDown(bloc.close);
      final states = <SecurityState>[];
      final sub = bloc.stream.listen(states.add);
      bloc.add(const SecurityEvent.sessionsRequested());
      await _flush();
      bloc.add(const SecurityEvent.mfaEnrollmentRequested());
      await _flush();
      expect((states.last as SecurityLoaded).mfaStatus,
          isA<MfaEnrollmentPending>());

      bloc.add(const SecurityEvent.mfaEnrollmentCancelled());
      await _flush();

      expect(
          (states.last as SecurityLoaded).mfaStatus, isA<MfaEnrollmentIdle>());
      expect(states.last.toString().contains('BASE32SECRET'), isFalse);
      verifyNever(() => repository.completeMfaEnrollment(
          mfaMethodId: any(named: 'mfaMethodId'), code: any(named: 'code')));
      await sub.cancel();
    });

    test('enrollment start failure stays a retryable failure', () async {
      when(() => repository.sessions()).thenAnswer((_) async => <SessionDto>[]);
      when(() => repository.beginMfaEnrollment())
          .thenThrow(const NetworkException('offline'));

      final bloc = SecurityBloc(repository);
      addTearDown(bloc.close);
      final states = <SecurityState>[];
      final sub = bloc.stream.listen(states.add);
      bloc.add(const SecurityEvent.sessionsRequested());
      await _flush();
      bloc.add(const SecurityEvent.mfaEnrollmentRequested());
      await _flush();

      final failed = (states.last as SecurityLoaded).mfaStatus;
      expect(failed, isA<MfaEnrollmentFailed>());
      expect((failed as MfaEnrollmentFailed).error, isA<NetworkException>());
      expect(states.last.toString().contains('BASE32SECRET'), isFalse);
      await sub.cancel();
    });

    test(
        'a completion failure stays a localized retryable failure and never '
        'disables MFA', () async {
      when(() => repository.sessions()).thenAnswer((_) async => <SessionDto>[]);
      when(() => repository.beginMfaEnrollment())
          .thenAnswer((_) async => _enrollment());
      when(() => repository.completeMfaEnrollment(
          mfaMethodId: any(named: 'mfaMethodId'),
          code: any(named: 'code'))).thenThrow(const UnauthorizedException(
        'bad code',
        messageKey: 'mfa_invalidCode',
        statusCode: 400,
      ));

      final bloc = SecurityBloc(repository);
      addTearDown(bloc.close);
      final states = <SecurityState>[];
      final sub = bloc.stream.listen(states.add);
      bloc.add(const SecurityEvent.sessionsRequested());
      await _flush();
      bloc.add(const SecurityEvent.mfaEnrollmentRequested());
      await _flush();
      bloc.add(const SecurityEvent.mfaEnrollmentCompleted(code: '000000'));
      await _flush();

      final failed = (states.last as SecurityLoaded).mfaStatus;
      expect(failed, isA<MfaEnrollmentFailed>());
      expect(
          (failed as MfaEnrollmentFailed).error!.messageKey, 'mfa_invalidCode');
      expect(states.last.toString().contains('BASE32SECRET'), isFalse);
      await sub.cancel();
    });

    test('a closed bloc never emits after an in-flight enrollment completes',
        () async {
      final gate = Completer<MfaEnrollmentDto>();
      when(() => repository.beginMfaEnrollment())
          .thenAnswer((_) => gate.future);

      final bloc = SecurityBloc(repository);
      bloc.add(const SecurityEvent.mfaEnrollmentRequested());
      await pumpEventQueue();
      await bloc.close();

      gate.complete(_enrollment());
      await _flush();
      expect(bloc.isClosed, isTrue);
    });
  });
}
