import 'dart:async';

import 'package:flutter_test/flutter_test.dart';
import 'package:member_portal/features/auth/domain/auth_models.dart';
import 'package:member_portal/features/member/application/member_session_bloc.dart';
import 'package:member_portal/features/member/application/member_session_event.dart';
import 'package:member_portal/features/member/application/member_session_state.dart';
import 'package:member_portal/features/member/data/member_dtos.dart';
import 'package:member_portal/features/member/domain/member_models.dart'
    hide MemberSessionUnlinked, MemberSessionForbidden, MemberSessionFailed;
import 'package:member_portal/features/member/domain/member_repository.dart';
import 'package:mocktail/mocktail.dart';

class _MockMemberRepository extends Mock implements MemberRepository {}

const _accountA = AuthUser(userAccountId: 'u1', email: 'ada@example.org');
const _accountB = AuthUser(userAccountId: 'u2', email: 'grace@example.org');

PersonDto _person(String id, String name) => PersonDto(
      id: id,
      preferredName: name,
      status: 'Active',
      hasLinkedIdentityAccount: true,
      createdOn: DateTime(2020, 1, 1),
    );

MembershipDto _membership({String status = 'Active'}) => MembershipDto(
      id: 'm1',
      personId: 'p1',
      status: status,
      effectiveFrom: DateTime(2021, 3, 1),
    );

Future<void> _flush() async {
  await pumpEventQueue();
  await Future<void>.delayed(Duration.zero);
  await pumpEventQueue();
}

void main() {
  late _MockMemberRepository repository;

  setUp(() {
    repository = _MockMemberRepository();
  });

  group('MemberSessionBloc bootstrap', () {
    test('resolves to ready with account, person and membership', () async {
      final persona = _person('p1', 'Ada');
      when(() => repository.loadMemberSession()).thenAnswer(
        (_) async => MemberSessionResult.resolved(
          account: _accountA,
          person: persona,
          membership: _membership(),
        ),
      );

      final bloc = MemberSessionBloc(repository);
      addTearDown(bloc.close);
      final states = <MemberSessionState>[];
      final sub = bloc.stream.listen(states.add);
      bloc.add(const MemberSessionEvent.bootstrapRequested());
      await _flush();

      expect(states.last, isA<MemberSessionReady>());
      final ready = states.last as MemberSessionReady;
      expect(ready.context.account.userAccountId, 'u1');
      expect(ready.context.person.id, 'p1');
      expect(ready.context.membership!.status, 'Active');
      await sub.cancel();
    });

    for (final status in ['Pending', 'Suspended', 'Lapsed', 'Withdrawn']) {
      test('preserves the $status membership lifecycle on ready', () async {
        when(() => repository.loadMemberSession()).thenAnswer(
          (_) async => MemberSessionResult.resolved(
            account: _accountA,
            person: _person('p1', 'Ada'),
            membership: _membership(status: status),
          ),
        );

        final bloc = MemberSessionBloc(repository);
        addTearDown(bloc.close);
        final states = <MemberSessionState>[];
        final sub = bloc.stream.listen(states.add);
        bloc.add(const MemberSessionEvent.bootstrapRequested());
        await _flush();

        final ready = states.last as MemberSessionReady;
        expect(ready.context.membership!.status, status);
        await sub.cancel();
      });
    }

    test('an unlinked result maps to unlinked', () async {
      when(() => repository.loadMemberSession())
          .thenAnswer((_) async => const MemberSessionResult.unlinked());

      final bloc = MemberSessionBloc(repository);
      addTearDown(bloc.close);
      final states = <MemberSessionState>[];
      final sub = bloc.stream.listen(states.add);
      bloc.add(const MemberSessionEvent.bootstrapRequested());
      await _flush();

      expect(states.last, isA<MemberSessionUnlinked>());
      await sub.cancel();
    });

    test('an unauthenticated result maps to sessionExpired', () async {
      when(() => repository.loadMemberSession())
          .thenAnswer((_) async => const MemberSessionResult.unauthenticated());

      final bloc = MemberSessionBloc(repository);
      addTearDown(bloc.close);
      final states = <MemberSessionState>[];
      final sub = bloc.stream.listen(states.add);
      bloc.add(const MemberSessionEvent.bootstrapRequested());
      await _flush();

      expect(states.last, isA<MemberSessionExpired>());
      await sub.cancel();
    });

    test('a forbidden result maps to forbidden and exposes the keyed error',
        () async {
      when(() => repository.loadMemberSession()).thenAnswer(
        (_) async => const MemberSessionResult.forbidden(
          ForbiddenException('nope', messageKey: 'profile_forbidden'),
        ),
      );

      final bloc = MemberSessionBloc(repository);
      addTearDown(bloc.close);
      final states = <MemberSessionState>[];
      final sub = bloc.stream.listen(states.add);
      bloc.add(const MemberSessionEvent.bootstrapRequested());
      await _flush();

      expect(states.last, isA<MemberSessionForbidden>());
      expect(
        (states.last as MemberSessionForbidden).error.messageKey,
        'profile_forbidden',
      );
      await sub.cancel();
    });

    test('a network failure maps to failed', () async {
      when(() => repository.loadMemberSession()).thenAnswer(
        (_) async =>
            const MemberSessionResult.failed(NetworkException('offline')),
      );

      final bloc = MemberSessionBloc(repository);
      addTearDown(bloc.close);
      final states = <MemberSessionState>[];
      final sub = bloc.stream.listen(states.add);
      bloc.add(const MemberSessionEvent.bootstrapRequested());
      await _flush();

      expect(states.last, isA<MemberSessionFailed>());
      await sub.cancel();
    });

    test('a server failure maps to failed, and never to a ready state',
        () async {
      when(() => repository.loadMemberSession()).thenAnswer(
        (_) async => const MemberSessionResult.failed(ServerException('boom')),
      );

      final bloc = MemberSessionBloc(repository);
      addTearDown(bloc.close);
      final states = <MemberSessionState>[];
      final sub = bloc.stream.listen(states.add);
      bloc.add(const MemberSessionEvent.bootstrapRequested());
      await _flush();

      expect(states.last, isA<MemberSessionFailed>());
      await sub.cancel();
    });
  });

  group('MemberSessionBloc session isolation', () {
    test('a fresh session bloc never inherits the prior account', () async {
      when(() => repository.loadMemberSession()).thenAnswer(
        (_) async => MemberSessionResult.resolved(
          account: _accountA,
          person: _person('p1', 'Ada'),
          membership: null,
        ),
      );

      final blocA = MemberSessionBloc(repository);
      final statesA = <MemberSessionState>[];
      final subA = blocA.stream.listen(statesA.add);
      blocA.add(const MemberSessionEvent.bootstrapRequested());
      await _flush();
      expect(
        (statesA.last as MemberSessionReady).context.person.id,
        'p1',
      );
      await subA.cancel();
      await blocA.close();

      when(() => repository.loadMemberSession()).thenAnswer(
        (_) async => MemberSessionResult.resolved(
          account: _accountB,
          person: _person('p2', 'Grace'),
          membership: null,
        ),
      );

      final blocB = MemberSessionBloc(repository);
      addTearDown(blocB.close);
      final statesB = <MemberSessionState>[];
      final subB = blocB.stream.listen(statesB.add);
      blocB.add(const MemberSessionEvent.bootstrapRequested());
      await _flush();

      final ready = statesB.last as MemberSessionReady;
      expect(ready.context.account.userAccountId, 'u2');
      expect(ready.context.person.id, 'p2');
      expect(ready.context.person.preferredName, 'Grace');
      await subB.cancel();
    });

    test(
        'an in-flight bootstrap of a closed bloc never publishes its result '
        'into a new session', () async {
      final gate = Completer<MemberSessionResult>();
      when(() => repository.loadMemberSession()).thenAnswer((_) => gate.future);

      final blocA = MemberSessionBloc(repository);
      blocA.add(const MemberSessionEvent.bootstrapRequested());
      await pumpEventQueue();

      // Sign-out tears the shell down, closing its session bloc.
      await blocA.close();

      // Account B signs in: a fresh bloc bootstraps immediately.
      when(() => repository.loadMemberSession()).thenAnswer(
        (_) async => MemberSessionResult.resolved(
          account: _accountB,
          person: _person('p2', 'Grace'),
          membership: null,
        ),
      );
      final blocB = MemberSessionBloc(repository);
      addTearDown(blocB.close);
      blocB.add(const MemberSessionEvent.bootstrapRequested());
      await _flush();
      expect(
        (blocB.state as MemberSessionReady).context.person.preferredName,
        'Grace',
      );

      // Account A's in-flight response now completes. The closed bloc must not
      // emit (a StateError would otherwise surface as an uncaught error and
      // fail the test), and account B's state must remain untouched.
      gate.complete(
        MemberSessionResult.resolved(
          account: _accountA,
          person: _person('p1', 'Ada'),
          membership: null,
        ),
      );
      await _flush();

      expect(blocB.state, isA<MemberSessionReady>());
      expect(
        (blocB.state as MemberSessionReady).context.person.preferredName,
        'Grace',
      );
    });
  });
}
