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

    test(
        'a successful revoke marks the session in-flight, confirms on 204, and '
        'reloads the authoritative list', () async {
      when(() => repository.revokeSession('s1')).thenAnswer((_) async {});
      var readCount = 0;
      when(() => repository.sessions()).thenAnswer((_) async {
        readCount++;
        if (readCount == 1) return [_session()];
        return <SessionDto>[];
      });

      final bloc = SecurityBloc(repository);
      addTearDown(bloc.close);
      final states = <SecurityState>[];
      final sub = bloc.stream.listen(states.add);
      bloc.add(const SecurityEvent.sessionsRequested());
      await _flush();
      expect((states.last as SecurityLoaded).sessions, hasLength(1));

      bloc.add(const SecurityEvent.sessionRevokeRequested('s1'));
      await _flush();

      final loaded = states.last as SecurityLoaded;
      // The authorized `/me/sessions` re-fetch is the only removal source.
      expect(loaded.sessions, isEmpty);
      expect(loaded.revokedSessionIds, {'s1'});
      expect(loaded.revokingSessionIds, isEmpty);
      expect(loaded.sessionsError, isNull);
      verify(() => repository.revokeSession('s1')).called(1);
      verify(() => repository.sessions()).called(2);
      await sub.cancel();
    });

    test(
        'a mutation that succeeds but whose list reload fails keeps the stale '
        'list and the success, exposing a retryable list error', () async {
      when(() => repository.revokeSession('s1')).thenAnswer((_) async {});
      var readCount = 0;
      when(() => repository.sessions()).thenAnswer((_) async {
        readCount++;
        if (readCount == 1) return [_session()];
        throw const ServerException('list reload failed');
      });

      final bloc = SecurityBloc(repository);
      addTearDown(bloc.close);
      final states = <SecurityState>[];
      final sub = bloc.stream.listen(states.add);
      bloc.add(const SecurityEvent.sessionsRequested());
      await _flush();
      bloc.add(const SecurityEvent.sessionRevokeRequested('s1'));
      await _flush();

      final loaded = states.last as SecurityLoaded;
      expect(loaded.revokedSessionIds, {'s1'});
      // The stale list is kept rather than fabricated as empty.
      expect(loaded.sessions, hasLength(1));
      expect(loaded.sessionsError, isA<ServerException>());
      expect(loaded.isSessionsLoading, isFalse);
      await sub.cancel();
    });

    test(
        'a failed revoke keeps the session visible and retryable with the '
        'typed error, and never reports success', () async {
      when(() => repository.sessions()).thenAnswer((_) async => [_session()]);
      when(() => repository.revokeSession('s1')).thenThrow(
        const NotFoundException('The session was not found.', statusCode: 404),
      );

      final bloc = SecurityBloc(repository);
      addTearDown(bloc.close);
      final states = <SecurityState>[];
      final sub = bloc.stream.listen(states.add);
      bloc.add(const SecurityEvent.sessionsRequested());
      await _flush();
      bloc.add(const SecurityEvent.sessionRevokeRequested('s1'));
      await _flush();

      final loaded = states.last as SecurityLoaded;
      expect(loaded.sessions, hasLength(1));
      expect(loaded.revokedSessionIds, isEmpty);
      expect(loaded.sessionRevokeErrors['s1'], isA<NotFoundException>());
      expect(loaded.revokingSessionIds, isEmpty);
      verify(() => repository.revokeSession('s1')).called(1);
      // No reload happened for a failed mutation.
      verify(() => repository.sessions()).called(1);
      await sub.cancel();
    });

    test('a 401 revoke surfaces as UnauthorizedException, not a refresh',
        () async {
      when(() => repository.sessions()).thenAnswer((_) async => [_session()]);
      when(() => repository.revokeSession('s1')).thenThrow(
        const UnauthorizedException('token invalid', statusCode: 401),
      );

      final bloc = SecurityBloc(repository);
      addTearDown(bloc.close);
      final states = <SecurityState>[];
      final sub = bloc.stream.listen(states.add);
      bloc.add(const SecurityEvent.sessionsRequested());
      await _flush();
      bloc.add(const SecurityEvent.sessionRevokeRequested('s1'));
      await _flush();

      final loaded = states.last as SecurityLoaded;
      expect(loaded.sessionRevokeErrors['s1'], isA<UnauthorizedException>());
      expect(loaded.revokedSessionIds, isEmpty);
      expect(loaded.sessions, hasLength(1));
      await sub.cancel();
    });

    test('a duplicate revoke while in flight is ignored', () async {
      when(() => repository.sessions()).thenAnswer((_) async => [_session()]);
      final gate = Completer<void>();
      when(() => repository.revokeSession('s1')).thenAnswer((_) => gate.future);

      final bloc = SecurityBloc(repository);
      addTearDown(bloc.close);
      final states = <SecurityState>[];
      final sub = bloc.stream.listen(states.add);
      bloc.add(const SecurityEvent.sessionsRequested());
      await _flush();

      bloc.add(const SecurityEvent.sessionRevokeRequested('s1'));
      await pumpEventQueue();
      bloc.add(const SecurityEvent.sessionRevokeRequested('s1'));
      await pumpEventQueue();

      expect(
        (bloc.state as SecurityLoaded).revokingSessionIds,
        contains('s1'),
      );
      gate.complete();
      await _flush();
      verify(() => repository.revokeSession('s1')).called(1);
      await sub.cancel();
    });

    test('concurrent revokes of different sessions stay independent', () async {
      when(() => repository.sessions()).thenAnswer((_) async => [
            _session(),
            _session(id: 's2', deviceId: 'd2'),
          ]);
      when(() => repository.revokeSession('s1')).thenAnswer((_) async {});
      when(() => repository.revokeSession('s2'))
          .thenThrow(const NetworkException('offline'));

      final bloc = SecurityBloc(repository);
      addTearDown(bloc.close);
      final states = <SecurityState>[];
      final sub = bloc.stream.listen(states.add);
      bloc.add(const SecurityEvent.sessionsRequested());
      await _flush();
      bloc.add(const SecurityEvent.sessionRevokeRequested('s1'));
      await _flush();
      bloc.add(const SecurityEvent.sessionRevokeRequested('s2'));
      await _flush();

      final loaded = states.last as SecurityLoaded;
      expect(loaded.revokedSessionIds, {'s1'});
      expect(loaded.sessionRevokeErrors.keys, {'s2'});
      expect(loaded.revokingSessionIds, isEmpty);
      verify(() => repository.revokeSession('s1')).called(1);
      verify(() => repository.revokeSession('s2')).called(1);
      await sub.cancel();
    });

    test(
        'retrying after a failure clears the per-session error and completes '
        'on the second attempt', () async {
      var readCount = 0;
      when(() => repository.sessions()).thenAnswer((_) async {
        readCount++;
        if (readCount == 1) return [_session()];
        return <SessionDto>[];
      });
      var attempts = 0;
      when(() => repository.revokeSession('s1')).thenAnswer((_) async {
        attempts++;
        if (attempts == 1) throw const NetworkException('offline');
      });

      final bloc = SecurityBloc(repository);
      addTearDown(bloc.close);
      final states = <SecurityState>[];
      final sub = bloc.stream.listen(states.add);
      bloc.add(const SecurityEvent.sessionsRequested());
      await _flush();
      bloc.add(const SecurityEvent.sessionRevokeRequested('s1'));
      await _flush();
      expect((states.last as SecurityLoaded).sessionRevokeErrors, isNotEmpty);

      bloc.add(const SecurityEvent.sessionRevokeRequested('s1'));
      await _flush();

      final loaded = states.last as SecurityLoaded;
      expect(loaded.revokedSessionIds, {'s1'});
      expect(loaded.sessionRevokeErrors, isEmpty);
      expect(loaded.sessions, isEmpty);
      verify(() => repository.revokeSession('s1')).called(2);
      await sub.cancel();
    });

    test('a closed bloc never emits after an in-flight revoke completes',
        () async {
      when(() => repository.sessions()).thenAnswer((_) async => [_session()]);
      final gate = Completer<void>();
      when(() => repository.revokeSession('s1')).thenAnswer((_) => gate.future);

      final bloc = SecurityBloc(repository);
      bloc.add(const SecurityEvent.sessionsRequested());
      await pumpEventQueue();
      bloc.add(const SecurityEvent.sessionRevokeRequested('s1'));
      await pumpEventQueue();
      await bloc.close();

      gate.complete();
      await _flush();
      expect(bloc.isClosed, isTrue);
    });

    // ── Revoke others ─────────────────────────────────────────────────────

    test(
        'a successful revoke-others marks in-flight, confirms on 204, and '
        'reloads the authoritative session list', () async {
      when(() => repository.revokeOtherSessions()).thenAnswer((_) async {});
      var readCount = 0;
      when(() => repository.sessions()).thenAnswer((_) async {
        readCount++;
        if (readCount == 1) {
          return [_session(), _session(id: 's2', deviceId: 'd2')];
        }
        // The backend preserves the current family; the authoritative list
        // comes back with the current session only.
        return [_session()];
      });

      final bloc = SecurityBloc(repository);
      addTearDown(bloc.close);
      final states = <SecurityState>[];
      final sub = bloc.stream.listen(states.add);
      bloc.add(const SecurityEvent.sessionsRequested());
      await _flush();
      expect((states.last as SecurityLoaded).sessions, hasLength(2));

      bloc.add(const SecurityEvent.revokeOthersRequested());
      await _flush();

      final loaded = states.last as SecurityLoaded;
      expect(
        states.any((s) => s is SecurityLoaded && s.revokingOthers),
        isTrue,
        reason: 'an intermediate in-flight state must be emitted',
      );
      expect(loaded.revokeOthersSucceeded, isTrue);
      expect(loaded.revokeOthersError, isNull);
      expect(loaded.sessions, hasLength(1));
      verify(() => repository.revokeOtherSessions()).called(1);
      verify(() => repository.sessions()).called(2);
      await sub.cancel();
    });

    test('a duplicate revoke-others while in flight is ignored', () async {
      when(() => repository.sessions())
          .thenAnswer((_) async => [_session(), _session(id: 's2')]);
      final gate = Completer<void>();
      when(() => repository.revokeOtherSessions())
          .thenAnswer((_) => gate.future);

      final bloc = SecurityBloc(repository);
      addTearDown(bloc.close);
      final states = <SecurityState>[];
      final sub = bloc.stream.listen(states.add);
      bloc.add(const SecurityEvent.sessionsRequested());
      await _flush();

      bloc.add(const SecurityEvent.revokeOthersRequested());
      await pumpEventQueue();
      bloc.add(const SecurityEvent.revokeOthersRequested());
      await pumpEventQueue();

      expect((bloc.state as SecurityLoaded).revokingOthers, isTrue);
      gate.complete();
      await _flush();
      verify(() => repository.revokeOtherSessions()).called(1);
      await sub.cancel();
    });

    test(
        'a failed revoke-others keeps the operation retryable and never '
        'reports success', () async {
      when(() => repository.sessions())
          .thenAnswer((_) async => [_session(), _session(id: 's2')]);
      when(() => repository.revokeOtherSessions())
          .thenThrow(const NetworkException('offline'));

      final bloc = SecurityBloc(repository);
      addTearDown(bloc.close);
      final states = <SecurityState>[];
      final sub = bloc.stream.listen(states.add);
      bloc.add(const SecurityEvent.sessionsRequested());
      await _flush();
      bloc.add(const SecurityEvent.revokeOthersRequested());
      await _flush();

      final loaded = states.last as SecurityLoaded;
      expect(loaded.revokeOthersError, isA<NetworkException>());
      expect(loaded.revokeOthersSucceeded, isFalse);
      expect(loaded.revokingOthers, isFalse);
      // A failed mutation never triggers a list reload.
      verify(() => repository.sessions()).called(1);
      await sub.cancel();
    });

    test('a 401 revoke-others surfaces as UnauthorizedException, not success',
        () async {
      when(() => repository.sessions())
          .thenAnswer((_) async => [_session(), _session(id: 's2')]);
      when(() => repository.revokeOtherSessions()).thenThrow(
        const UnauthorizedException('token invalid', statusCode: 401),
      );

      final bloc = SecurityBloc(repository);
      addTearDown(bloc.close);
      final states = <SecurityState>[];
      final sub = bloc.stream.listen(states.add);
      bloc.add(const SecurityEvent.sessionsRequested());
      await _flush();
      bloc.add(const SecurityEvent.revokeOthersRequested());
      await _flush();

      final loaded = states.last as SecurityLoaded;
      expect(loaded.revokeOthersError, isA<UnauthorizedException>());
      expect(loaded.revokeOthersSucceeded, isFalse);
      await sub.cancel();
    });

    test(
        'retrying a failed revoke-others clears the error and completes on '
        'the second attempt', () async {
      when(() => repository.sessions())
          .thenAnswer((_) async => [_session(), _session(id: 's2')]);
      var attempts = 0;
      when(() => repository.revokeOtherSessions()).thenAnswer((_) async {
        attempts++;
        if (attempts == 1) throw const NetworkException('offline');
        return;
      });
      var readCount = 0;
      when(() => repository.sessions()).thenAnswer((_) async {
        readCount++;
        if (readCount == 1) return [_session(), _session(id: 's2')];
        return [_session()];
      });

      final bloc = SecurityBloc(repository);
      addTearDown(bloc.close);
      final states = <SecurityState>[];
      final sub = bloc.stream.listen(states.add);
      bloc.add(const SecurityEvent.sessionsRequested());
      await _flush();
      bloc.add(const SecurityEvent.revokeOthersRequested());
      await _flush();
      expect(
        (states.last as SecurityLoaded).revokeOthersError,
        isA<NetworkException>(),
      );

      bloc.add(const SecurityEvent.revokeOthersRequested());
      await _flush();

      final loaded = states.last as SecurityLoaded;
      expect(loaded.revokeOthersError, isNull);
      expect(loaded.revokeOthersSucceeded, isTrue);
      verify(() => repository.revokeOtherSessions()).called(2);
      await sub.cancel();
    });

    test('a closed bloc never emits after an in-flight revoke-others completes',
        () async {
      when(() => repository.sessions()).thenAnswer((_) async => <SessionDto>[]);
      final gate = Completer<void>();
      when(() => repository.revokeOtherSessions())
          .thenAnswer((_) => gate.future);

      final bloc = SecurityBloc(repository);
      bloc.add(const SecurityEvent.sessionsRequested());
      await pumpEventQueue();
      bloc.add(const SecurityEvent.revokeOthersRequested());
      await pumpEventQueue();
      await bloc.close();

      gate.complete();
      await _flush();
      expect(bloc.isClosed, isTrue);
    });

    // ── MFA removal ──────────────────────────────────────────────────────

    test('successful mfa removal transitions through inProgress → succeeded',
        () async {
      when(() => repository.sessions()).thenAnswer((_) async => <SessionDto>[]);
      when(() => repository.removeMfaMethod('m1')).thenAnswer((_) async {});

      final bloc = SecurityBloc(repository);
      addTearDown(bloc.close);
      final states = <SecurityState>[];
      final sub = bloc.stream.listen(states.add);
      bloc.add(const SecurityEvent.sessionsRequested());
      await _flush();

      bloc.add(const SecurityEvent.mfaRemovalRequested('m1'));
      await _flush();

      final loaded = states.last as SecurityLoaded;
      expect(loaded.mfaRemovalStatus, isA<MfaRemovalSucceeded>());
      verify(() => repository.removeMfaMethod('m1')).called(1);
      await sub.cancel();
    });

    test('failed mfa removal surfaces error and retains method id', () async {
      when(() => repository.sessions()).thenAnswer((_) async => <SessionDto>[]);
      when(() => repository.removeMfaMethod('m1')).thenThrow(
        const ValidationException('final mfa', statusCode: 409),
      );

      final bloc = SecurityBloc(repository);
      addTearDown(bloc.close);
      final states = <SecurityState>[];
      final sub = bloc.stream.listen(states.add);
      bloc.add(const SecurityEvent.sessionsRequested());
      await _flush();

      bloc.add(const SecurityEvent.mfaRemovalRequested('m1'));
      await _flush();

      final loaded = states.last as SecurityLoaded;
      final failed = loaded.mfaRemovalStatus as MfaRemovalFailed;
      expect(failed.methodId, 'm1');
      expect(failed.error, isA<ValidationException>());
      await sub.cancel();
    });

    test('a duplicate mfa removal while in flight is ignored', () async {
      when(() => repository.sessions()).thenAnswer((_) async => <SessionDto>[]);
      final gate = Completer<void>();
      when(() => repository.removeMfaMethod('m1'))
          .thenAnswer((_) => gate.future);

      final bloc = SecurityBloc(repository);
      addTearDown(bloc.close);
      final states = <SecurityState>[];
      final sub = bloc.stream.listen(states.add);
      bloc.add(const SecurityEvent.sessionsRequested());
      await _flush();

      bloc.add(const SecurityEvent.mfaRemovalRequested('m1'));
      await pumpEventQueue();
      bloc.add(const SecurityEvent.mfaRemovalRequested('m1'));
      await pumpEventQueue();

      gate.complete();
      await _flush();

      // The repository call happened only once.
      verify(() => repository.removeMfaMethod('m1')).called(1);
      await sub.cancel();
    });

    test('cancelling a failed mfa removal returns the section to idle',
        () async {
      when(() => repository.sessions()).thenAnswer((_) async => <SessionDto>[]);
      when(() => repository.removeMfaMethod('m1')).thenThrow(
        const ValidationException('final mfa', statusCode: 409),
      );

      final bloc = SecurityBloc(repository);
      addTearDown(bloc.close);
      final states = <SecurityState>[];
      final sub = bloc.stream.listen(states.add);
      bloc.add(const SecurityEvent.sessionsRequested());
      await _flush();

      bloc.add(const SecurityEvent.mfaRemovalRequested('m1'));
      await _flush();
      expect(
        (states.last as SecurityLoaded).mfaRemovalStatus,
        isA<MfaRemovalFailed>(),
      );

      bloc.add(const SecurityEvent.mfaRemovalCancelled());
      await _flush();

      final loaded = states.last as SecurityLoaded;
      // The failure view is dismissed; removal is idle, no success is
      // fabricated, and the session list is untouched.
      expect(loaded.mfaRemovalStatus, isA<MfaRemovalIdle>());
      expect(loaded.mfaStatus, isA<MfaEnrollmentIdle>());
      expect(loaded.sessions, isEmpty);
      verify(() => repository.removeMfaMethod('m1')).called(1);
      await sub.cancel();
    });
  });
}
