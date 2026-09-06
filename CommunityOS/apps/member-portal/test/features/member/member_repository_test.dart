import 'package:dio/dio.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:member_portal/core/error/app_exception.dart';
import 'package:member_portal/core/network/error_mapper.dart';
import 'package:member_portal/features/auth/data/auth_api.dart';
import 'package:member_portal/features/auth/data/auth_dtos.dart';
import 'package:member_portal/features/member/data/member_api.dart';
import 'package:member_portal/features/member/data/member_dtos.dart';
import 'package:member_portal/features/member/domain/member_models.dart';
import 'package:member_portal/features/member/domain/member_repository.dart';
import 'package:mocktail/mocktail.dart';

class _MockAccountApi extends Mock implements AccountApi {}

class _MockMemberApi extends Mock implements MemberApi {}

UserAccountDto _account() => UserAccountDto(
      id: 'u1',
      email: 'ada@example.org',
      status: 'Active',
      createdOn: DateTime(2020, 1, 1),
    );

PersonDto _person() => PersonDto(
      id: 'p1',
      preferredName: 'Ada',
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

DioException _http(int status, {String path = '/api/v1/my-person'}) =>
    DioException(
      requestOptions: RequestOptions(path: path),
      type: DioExceptionType.badResponse,
      response: Response<dynamic>(
        requestOptions: RequestOptions(path: path),
        statusCode: status,
        data: {'detail': 'nope'},
      ),
    );

void main() {
  late _MockAccountApi accountApi;
  late _MockMemberApi api;
  late MemberRepository repository;

  setUp(() {
    accountApi = _MockAccountApi();
    api = _MockMemberApi();
    repository = MemberRepository(accountApi, api, const ErrorMapper());
  });

  group('loadMemberSession', () {
    test('resolves account, person and membership', () async {
      when(() => accountApi.me()).thenAnswer((_) async => _account());
      when(() => api.getMyPerson()).thenAnswer((_) async => _person());
      when(() => api.getMembershipByPerson('p1'))
          .thenAnswer((_) async => _membership());

      final result = await repository.loadMemberSession();

      expect(result, isA<MemberSessionResolved>());
      final resolved = result as MemberSessionResolved;
      expect(resolved.account.userAccountId, 'u1');
      expect(resolved.person.id, 'p1');
      expect(resolved.membership!.status, 'Active');
    });

    test('a null membership payload is the legitimate no-membership state',
        () async {
      when(() => accountApi.me()).thenAnswer((_) async => _account());
      when(() => api.getMyPerson()).thenAnswer((_) async => _person());
      when(() => api.getMembershipByPerson('p1')).thenAnswer((_) async => null);

      final result = await repository.loadMemberSession();

      final resolved = result as MemberSessionResolved;
      expect(resolved.person.id, 'p1');
      expect(resolved.membership, isNull);
    });

    for (final status in ['Pending', 'Suspended', 'Lapsed', 'Withdrawn']) {
      test('preserves the $status membership lifecycle status', () async {
        when(() => accountApi.me()).thenAnswer((_) async => _account());
        when(() => api.getMyPerson()).thenAnswer((_) async => _person());
        when(() => api.getMembershipByPerson('p1'))
            .thenAnswer((_) async => _membership(status: status));

        final result = await repository.loadMemberSession();

        final resolved = result as MemberSessionResolved;
        expect(resolved.membership!.status, status);
      });
    }

    test('a 404 from my-person maps to an unlinked account, not a logout',
        () async {
      when(() => accountApi.me()).thenAnswer((_) async => _account());
      when(() => api.getMyPerson()).thenThrow(_http(404));

      final result = await repository.loadMemberSession();

      expect(result, isA<MemberSessionUnlinked>());
    });

    test('a 401 from my-person maps to an unauthenticated session', () async {
      when(() => accountApi.me()).thenAnswer((_) async => _account());
      when(() => api.getMyPerson()).thenThrow(_http(401));

      final result = await repository.loadMemberSession();

      expect(result, isA<MemberSessionUnauthenticated>());
    });

    test('a 403 from my-person maps to a forbidden result with a key',
        () async {
      when(() => accountApi.me()).thenAnswer((_) async => _account());
      when(() => api.getMyPerson()).thenThrow(_http(403));

      final result = await repository.loadMemberSession();

      expect(result, isA<MemberSessionForbidden>());
      final error = (result as MemberSessionForbidden).error;
      expect(error, isA<ForbiddenException>());
      expect(error.messageKey, 'profile_forbidden');
    });

    test('a 401 from /me maps to an unauthenticated session', () async {
      when(() => accountApi.me()).thenThrow(_http(401, path: '/api/v1/me'));

      final result = await repository.loadMemberSession();

      expect(result, isA<MemberSessionUnauthenticated>());
    });

    test('a network failure from /me maps to a failed result', () async {
      when(() => accountApi.me()).thenThrow(
        DioException(
          requestOptions: RequestOptions(path: '/api/v1/me'),
          type: DioExceptionType.connectionError,
        ),
      );

      final result = await repository.loadMemberSession();

      expect(result, isA<MemberSessionFailed>());
    });

    test(
        'a 401 from the membership read maps to unauthenticated, NOT no-membership',
        () async {
      when(() => accountApi.me()).thenAnswer((_) async => _account());
      when(() => api.getMyPerson()).thenAnswer((_) async => _person());
      when(() => api.getMembershipByPerson('p1'))
          .thenThrow(_http(401, path: '/api/v1/memberships/by-person/p1'));

      final result = await repository.loadMemberSession();

      expect(result, isA<MemberSessionUnauthenticated>());
    });

    test('a 403 from the membership read maps to forbidden, NOT no-membership',
        () async {
      when(() => accountApi.me()).thenAnswer((_) async => _account());
      when(() => api.getMyPerson()).thenAnswer((_) async => _person());
      when(() => api.getMembershipByPerson('p1'))
          .thenThrow(_http(403, path: '/api/v1/memberships/by-person/p1'));

      final result = await repository.loadMemberSession();

      expect(result, isA<MemberSessionForbidden>());
    });

    test('a 500 from the membership read maps to failed, NOT no-membership',
        () async {
      when(() => accountApi.me()).thenAnswer((_) async => _account());
      when(() => api.getMyPerson()).thenAnswer((_) async => _person());
      when(() => api.getMembershipByPerson('p1'))
          .thenThrow(_http(500, path: '/api/v1/memberships/by-person/p1'));

      final result = await repository.loadMemberSession();

      expect(result, isA<MemberSessionFailed>());
      final error = (result as MemberSessionFailed).error;
      expect(error, isA<ServerException>());
    });

    test('a timeout from the membership read maps to failed', () async {
      when(() => accountApi.me()).thenAnswer((_) async => _account());
      when(() => api.getMyPerson()).thenAnswer((_) async => _person());
      when(() => api.getMembershipByPerson('p1')).thenThrow(
        DioException(
          requestOptions:
              RequestOptions(path: '/api/v1/memberships/by-person/p1'),
          type: DioExceptionType.connectionTimeout,
        ),
      );

      final result = await repository.loadMemberSession();

      expect(result, isA<MemberSessionFailed>());
      final error = (result as MemberSessionFailed).error;
      expect(error, isA<RequestTimeoutException>());
    });
  });

  group('personDetail', () {
    test('loads a person detail record', () async {
      final detail = PersonDetailDto(
        id: 'p1',
        preferredName: 'Ada',
        status: 'Active',
        profileVisibility: 'Self',
        contactVisibility: 'Self',
        dateOfBirthVisibility: 'Self',
        createdOn: DateTime(2020, 1, 1),
      );
      when(() => api.getPerson('p1')).thenAnswer((_) async => detail);

      final result = await repository.personDetail('p1');

      expect(result.preferredName, 'Ada');
    });

    test('a 404 person detail maps to NotFoundException', () async {
      when(() => api.getPerson('p1')).thenThrow(_http(404));

      expect(
        () => repository.personDetail('p1'),
        throwsA(isA<NotFoundException>()),
      );
    });
  });
}
