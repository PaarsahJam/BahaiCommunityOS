import 'dart:async';

import 'package:flutter_test/flutter_test.dart';
import 'package:member_portal/core/error/app_exception.dart';
import 'package:member_portal/features/member/application/profile_bloc.dart';
import 'package:member_portal/features/member/application/profile_event.dart';
import 'package:member_portal/features/member/application/profile_state.dart';
import 'package:member_portal/features/member/data/member_dtos.dart';
import 'package:member_portal/features/member/domain/member_repository.dart';
import 'package:mocktail/mocktail.dart';

class _MockMemberRepository extends Mock implements MemberRepository {}

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

  group('ProfileBloc', () {
    test('loads and exposes the person detail', () async {
      final detail = PersonDetailDto(
        id: 'p1',
        preferredName: 'Ada',
        status: 'Active',
        profileVisibility: 'Self',
        contactVisibility: 'Self',
        dateOfBirthVisibility: 'Self',
        createdOn: DateTime(2020, 1, 1),
      );
      when(() => repository.personDetail('p1')).thenAnswer((_) async => detail);

      final bloc = ProfileBloc(repository);
      addTearDown(bloc.close);
      final states = <ProfileState>[];
      final sub = bloc.stream.listen(states.add);
      bloc.add(const ProfileEvent.requested(personId: 'p1'));
      await _flush();

      expect(states, contains(isA<ProfileLoading>()));
      expect(states.last, isA<ProfileLoaded>());
      expect((states.last as ProfileLoaded).detail.id, 'p1');
      await sub.cancel();
    });

    test('maps a 404 to a notFound profile error', () async {
      when(() => repository.personDetail('p1')).thenThrow(
        const NotFoundException('missing'),
      );

      final bloc = ProfileBloc(repository);
      addTearDown(bloc.close);
      final states = <ProfileState>[];
      final sub = bloc.stream.listen(states.add);
      bloc.add(const ProfileEvent.requested(personId: 'p1'));
      await _flush();

      expect(states.last, isA<ProfileFailed>());
      final error = (states.last as ProfileFailed).error;
      expect(error, isA<NotFoundException>());
      expect(error.messageKey, 'profile_notFound');
      await sub.cancel();
    });

    test('maps a 403 to a forbidden profile error', () async {
      when(() => repository.personDetail('p1')).thenThrow(
        const ForbiddenException('nope'),
      );

      final bloc = ProfileBloc(repository);
      addTearDown(bloc.close);
      final states = <ProfileState>[];
      final sub = bloc.stream.listen(states.add);
      bloc.add(const ProfileEvent.requested(personId: 'p1'));
      await _flush();

      expect(states.last, isA<ProfileFailed>());
      final error = (states.last as ProfileFailed).error;
      expect(error, isA<ForbiddenException>());
      expect(error.messageKey, 'profile_forbidden');
      await sub.cancel();
    });

    test('keeps privacy-masked fields absent', () async {
      final masked = PersonDetailDto(
        id: 'p1',
        preferredName: 'Ada',
        status: 'Active',
        profileVisibility: 'Self',
        contactVisibility: 'None',
        dateOfBirthVisibility: 'None',
        createdOn: DateTime(2020, 1, 1),
      );
      when(() => repository.personDetail('p1')).thenAnswer((_) async => masked);

      final bloc = ProfileBloc(repository);
      addTearDown(bloc.close);
      final states = <ProfileState>[];
      final sub = bloc.stream.listen(states.add);
      bloc.add(const ProfileEvent.requested(personId: 'p1'));
      await _flush();

      final detail = (states.last as ProfileLoaded).detail;
      expect(detail.contactMethods, isEmpty);
      expect(detail.dateOfBirth, isNull);
      expect(detail.identityAccountId, isNull);
      await sub.cancel();
    });

    test('a closed bloc never emits after an in-flight load completes',
        () async {
      final gate = Completer<PersonDetailDto>();
      when(() => repository.personDetail('p1')).thenAnswer((_) => gate.future);

      final bloc = ProfileBloc(repository);
      bloc.add(const ProfileEvent.requested(personId: 'p1'));
      await pumpEventQueue();

      await bloc.close();

      gate.complete(
        PersonDetailDto(
          id: 'p1',
          preferredName: 'Ada',
          status: 'Active',
          profileVisibility: 'Self',
          contactVisibility: 'Self',
          dateOfBirthVisibility: 'Self',
          createdOn: DateTime(2020, 1, 1),
        ),
      );
      await _flush();

      expect(bloc.isClosed, isTrue);
    });
  });
}
