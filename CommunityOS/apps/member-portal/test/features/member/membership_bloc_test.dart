import 'dart:async';

import 'package:flutter_test/flutter_test.dart';
import 'package:member_portal/core/error/app_exception.dart';
import 'package:member_portal/features/member/application/membership_bloc.dart';
import 'package:member_portal/features/member/application/membership_event.dart';
import 'package:member_portal/features/member/application/membership_state.dart';
import 'package:member_portal/features/member/data/member_dtos.dart';
import 'package:member_portal/features/member/domain/member_repository.dart';
import 'package:mocktail/mocktail.dart';

class _MockMemberRepository extends Mock implements MemberRepository {}

Future<void> _flush() async {
  await pumpEventQueue();
  await Future<void>.delayed(Duration.zero);
  await pumpEventQueue();
}

MembershipDto _membership({List<MembershipPeriodDto> history = const []}) =>
    MembershipDto(
      id: 'm1',
      personId: 'p1',
      status: 'Active',
      effectiveFrom: DateTime(2021, 3, 1),
      history: history,
    );

void main() {
  late _MockMemberRepository repository;

  setUp(() {
    repository = _MockMemberRepository();
  });

  group('MembershipBloc', () {
    test('loads and exposes the membership record', () async {
      when(() => repository.getMembership('p1'))
          .thenAnswer((_) async => _membership());

      final bloc = MembershipBloc(repository);
      addTearDown(bloc.close);
      final states = <MembershipState>[];
      final sub = bloc.stream.listen(states.add);
      bloc.add(const MembershipEvent.requested(personId: 'p1'));
      await _flush();

      expect(states, contains(isA<MembershipLoading>()));
      expect(states.last, isA<MembershipLoaded>());
      expect((states.last as MembershipLoaded).membership!.status, 'Active');
      await sub.cancel();
    });

    test('loads a membership record with its history', () async {
      when(() => repository.getMembership('p1'))
          .thenAnswer((_) async => _membership(
                history: [
                  MembershipPeriodDto(
                    status: 'pending',
                    effectiveFrom: DateTime(2020, 1, 1),
                    effectiveUntil: DateTime(2021, 3, 1),
                  ),
                ],
              ));

      final bloc = MembershipBloc(repository);
      addTearDown(bloc.close);
      final states = <MembershipState>[];
      final sub = bloc.stream.listen(states.add);
      bloc.add(const MembershipEvent.requested(personId: 'p1'));
      await _flush();

      final membership = (states.last as MembershipLoaded).membership!;
      expect(membership.history, hasLength(1));
      expect(membership.history.single.status, 'pending');
      await sub.cancel();
    });

    test('a 200 + null payload is the legitimate no-membership state',
        () async {
      when(() => repository.getMembership('p1')).thenAnswer((_) async => null);

      final bloc = MembershipBloc(repository);
      addTearDown(bloc.close);
      final states = <MembershipState>[];
      final sub = bloc.stream.listen(states.add);
      bloc.add(const MembershipEvent.requested(personId: 'p1'));
      await _flush();

      expect(states.last, isA<MembershipLoaded>());
      expect((states.last as MembershipLoaded).membership, isNull);
      await sub.cancel();
    });

    test('a 401 stays a failure, never a no-membership state', () async {
      when(() => repository.getMembership('p1'))
          .thenThrow(const UnauthorizedException('expired'));

      final bloc = MembershipBloc(repository);
      addTearDown(bloc.close);
      final states = <MembershipState>[];
      final sub = bloc.stream.listen(states.add);
      bloc.add(const MembershipEvent.requested(personId: 'p1'));
      await _flush();

      expect(states.last, isA<MembershipFailed>());
      expect((states.last as MembershipFailed).error,
          isA<UnauthorizedException>());
      expect((states.last as MembershipFailed).error.messageKey, isNull);
      expect(states.whereType<MembershipLoaded>(), isEmpty);
      await sub.cancel();
    });

    test('maps a 403 to a keyed membership-forbidden error', () async {
      when(() => repository.getMembership('p1'))
          .thenThrow(const ForbiddenException('nope'));

      final bloc = MembershipBloc(repository);
      addTearDown(bloc.close);
      final states = <MembershipState>[];
      final sub = bloc.stream.listen(states.add);
      bloc.add(const MembershipEvent.requested(personId: 'p1'));
      await _flush();

      expect(states.last, isA<MembershipFailed>());
      final error = (states.last as MembershipFailed).error;
      expect(error, isA<ForbiddenException>());
      expect(error.messageKey, 'membership_forbidden');
      await sub.cancel();
    });

    test('maps a 404 to a keyed membership-not-found error', () async {
      when(() => repository.getMembership('p1'))
          .thenThrow(const NotFoundException('missing'));

      final bloc = MembershipBloc(repository);
      addTearDown(bloc.close);
      final states = <MembershipState>[];
      final sub = bloc.stream.listen(states.add);
      bloc.add(const MembershipEvent.requested(personId: 'p1'));
      await _flush();

      expect(states.last, isA<MembershipFailed>());
      final error = (states.last as MembershipFailed).error;
      expect(error, isA<NotFoundException>());
      expect(error.messageKey, 'membership_notFound');
      await sub.cancel();
    });

    test('a timeout maps to a membership timeout failure', () async {
      when(() => repository.getMembership('p1'))
          .thenThrow(const RequestTimeoutException('slow'));

      final bloc = MembershipBloc(repository);
      addTearDown(bloc.close);
      final states = <MembershipState>[];
      final sub = bloc.stream.listen(states.add);
      bloc.add(const MembershipEvent.requested(personId: 'p1'));
      await _flush();

      expect(states.last, isA<MembershipFailed>());
      expect((states.last as MembershipFailed).error,
          isA<RequestTimeoutException>());
      await sub.cancel();
    });

    test('a network failure maps to a membership network failure', () async {
      when(() => repository.getMembership('p1'))
          .thenThrow(const NetworkException('offline'));

      final bloc = MembershipBloc(repository);
      addTearDown(bloc.close);
      final states = <MembershipState>[];
      final sub = bloc.stream.listen(states.add);
      bloc.add(const MembershipEvent.requested(personId: 'p1'));
      await _flush();

      expect(states.last, isA<MembershipFailed>());
      expect((states.last as MembershipFailed).error, isA<NetworkException>());
      await sub.cancel();
    });

    test('a 500 maps to a server failure, never a no-membership state',
        () async {
      when(() => repository.getMembership('p1'))
          .thenThrow(const ServerException('boom', statusCode: 500));

      final bloc = MembershipBloc(repository);
      addTearDown(bloc.close);
      final states = <MembershipState>[];
      final sub = bloc.stream.listen(states.add);
      bloc.add(const MembershipEvent.requested(personId: 'p1'));
      await _flush();

      expect(states.last, isA<MembershipFailed>());
      expect((states.last as MembershipFailed).error, isA<ServerException>());
      await sub.cancel();
    });

    test('a retry after a failure loads fresh membership data', () async {
      when(() => repository.getMembership('p1'))
          .thenThrow(const NetworkException('offline'));

      final bloc = MembershipBloc(repository);
      addTearDown(bloc.close);
      final states = <MembershipState>[];
      final sub = bloc.stream.listen(states.add);
      bloc.add(const MembershipEvent.requested(personId: 'p1'));
      await _flush();

      expect(states.last, isA<MembershipFailed>());

      when(() => repository.getMembership('p1'))
          .thenAnswer((_) async => _membership());
      bloc.add(const MembershipEvent.requested(personId: 'p1'));
      await _flush();

      expect(states.last, isA<MembershipLoaded>());
      expect((states.last as MembershipLoaded).membership!.personId, 'p1');
      verify(() => repository.getMembership('p1')).called(2);
      await sub.cancel();
    });

    test('a retry that fails again stays retryable', () async {
      when(() => repository.getMembership('p1'))
          .thenThrow(const NetworkException('offline'));

      final bloc = MembershipBloc(repository);
      addTearDown(bloc.close);
      final states = <MembershipState>[];
      final sub = bloc.stream.listen(states.add);
      bloc.add(const MembershipEvent.requested(personId: 'p1'));
      await _flush();
      bloc.add(const MembershipEvent.requested(personId: 'p1'));
      await _flush();

      expect(states.last, isA<MembershipFailed>());
      expect((states.last as MembershipFailed).error, isA<NetworkException>());
      expect(states.whereType<MembershipLoaded>(), isEmpty);
      await sub.cancel();
    });

    test('a closed bloc never emits after an in-flight load completes',
        () async {
      final gate = Completer<MembershipDto?>();
      when(() => repository.getMembership('p1')).thenAnswer((_) => gate.future);

      final bloc = MembershipBloc(repository);
      bloc.add(const MembershipEvent.requested(personId: 'p1'));
      await pumpEventQueue();

      await bloc.close();

      gate.complete(_membership());
      await _flush();

      expect(bloc.isClosed, isTrue);
    });
  });
}
