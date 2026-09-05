import 'package:dio/dio.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:member_portal/core/error/app_exception.dart';
import 'package:member_portal/core/network/error_mapper.dart';
import 'package:member_portal/features/member/data/member_api.dart';
import 'package:member_portal/features/member/data/member_dtos.dart';
import 'package:member_portal/features/member/domain/member_models.dart';
import 'package:member_portal/features/member/domain/member_repository.dart';
import 'package:mocktail/mocktail.dart';

class _MockMemberApi extends Mock implements MemberApi {}

PersonDto _person() => PersonDto(
      id: 'p1',
      preferredName: 'Ada',
      status: 'Active',
      hasLinkedIdentityAccount: true,
      createdOn: DateTime(2020, 1, 1),
    );

MembershipDto _membership() => MembershipDto(
      id: 'm1',
      personId: 'p1',
      status: 'Active',
      effectiveFrom: DateTime(2021, 3, 1),
    );

DioException _http(int status) => DioException(
      requestOptions: RequestOptions(path: '/api/v1/me'),
      type: DioExceptionType.badResponse,
      response: Response<dynamic>(
        requestOptions: RequestOptions(path: '/api/v1/me'),
        statusCode: status,
        data: {'detail': 'nope'},
      ),
    );

void main() {
  late _MockMemberApi api;
  late MemberRepository repository;

  setUp(() {
    api = _MockMemberApi();
    repository = MemberRepository(api, const ErrorMapper());
  });

  group('MemberRepository', () {
    test('resolves person and membership into MemberHomeData', () async {
      when(() => api.getMyPerson()).thenAnswer((_) async => _person());
      when(() => api.getMembershipByPerson('p1'))
          .thenAnswer((_) async => _membership());

      final result = await repository.loadMemberHome();

      expect(result, isA<MemberHomeResolved>());
      final resolved = result as MemberHomeResolved;
      expect(resolved.person.id, 'p1');
      expect(resolved.membership!.status, 'Active');
      expect(resolved.membership, isNotNull);
    });

    test('a 404 from my-person maps to an unlinked account', () async {
      when(() => api.getMyPerson()).thenThrow(_http(404));

      final result = await repository.loadMemberHome();

      expect(result, isA<MemberHomeUnlinked>());
    });

    test('a 401 from my-person maps to an unauthenticated result', () async {
      when(() => api.getMyPerson()).thenThrow(_http(401));

      final result = await repository.loadMemberHome();

      expect(result, isA<MemberHomeUnauthenticated>());
    });

    test('a 403 from my-person maps to a forbidden result with localized key',
        () async {
      when(() => api.getMyPerson()).thenThrow(_http(403));

      final result = await repository.loadMemberHome();

      expect(result, isA<MemberHomeForbidden>());
      final error = (result as MemberHomeForbidden).error;
      expect(error.messageKey, 'profile_forbidden');
    });

    test('a failed membership read still resolves the person', () async {
      when(() => api.getMyPerson()).thenAnswer((_) async => _person());
      when(() => api.getMembershipByPerson('p1')).thenThrow(_http(500));

      final result = await repository.loadMemberHome();

      final resolved = result as MemberHomeResolved;
      expect(resolved.person.id, 'p1');
      expect(resolved.membership, isNull);
    });

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
