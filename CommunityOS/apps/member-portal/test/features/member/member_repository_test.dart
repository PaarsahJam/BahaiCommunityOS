import 'package:dio/dio.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:member_portal/core/error/app_exception.dart';
import 'package:member_portal/core/network/auth_interceptor.dart';
import 'package:member_portal/core/network/error_mapper.dart';
import 'package:member_portal/core/network/refresh_coordinator.dart';
import 'package:member_portal/core/storage/token_storage.dart';
import 'package:member_portal/features/account/domain/account_exceptions.dart';
import 'package:member_portal/features/auth/data/auth_api.dart';
import 'package:member_portal/features/auth/data/auth_dtos.dart';
import 'package:member_portal/features/member/data/member_api.dart';
import 'package:member_portal/features/member/data/member_dtos.dart';
import 'package:member_portal/features/member/domain/member_models.dart';
import 'package:member_portal/features/member/domain/member_repository.dart';
import 'package:mocktail/mocktail.dart';

class _MockAccountApi extends Mock implements AccountApi {}

class _MockMemberApi extends Mock implements MemberApi {}

class _MockRefreshCoordinator extends Mock implements RefreshCoordinator {}

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

/// A 401 carrying the JwtBearer challenge the gateway forwards verbatim.
DioException _jwt401() => DioException(
      requestOptions: RequestOptions(path: '/api/v1/me/password'),
      type: DioExceptionType.badResponse,
      response: Response<dynamic>(
        requestOptions: RequestOptions(path: '/api/v1/me/password'),
        statusCode: 401,
        headers: Headers.fromMap({
          'www-authenticate': [
            'Bearer error="invalid_token", error_description="The token expired"'
          ]
        }),
        data: {'detail': 'token expired'},
      ),
    );

/// A 400 problem-details payload as produced by FluentValidation
/// (`InvalidModelState`), carrying PascalCase `errors[]`.
DioException _validation400() => DioException(
      requestOptions: RequestOptions(path: '/api/v1/me/password'),
      type: DioExceptionType.badResponse,
      response: Response<dynamic>(
        requestOptions: RequestOptions(path: '/api/v1/me/password'),
        statusCode: 400,
        data: {
          'status': 400,
          'title': 'One or more validation errors occurred.',
          'errors': [
            {
              'propertyName': 'CurrentPassword',
              'errorMessage': 'The current password is required.'
            },
            {
              'propertyName': 'NewPassword',
              'errorMessage':
                  'The new password must be at least 12 characters long.'
            },
          ],
        },
      ),
    );

void main() {
  late _MockAccountApi accountApi;
  late _MockMemberApi api;
  late _MockRefreshCoordinator coordinator;
  late MemberRepository repository;

  setUpAll(() {
    registerFallbackValue(const ChangePasswordRequestDto(
      currentPassword: 'fallback',
      newPassword: 'fallback',
    ));
  });

  setUp(() {
    accountApi = _MockAccountApi();
    api = _MockMemberApi();
    coordinator = _MockRefreshCoordinator();
    repository =
        MemberRepository(accountApi, api, const ErrorMapper(), coordinator);
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

  group('loadAccount', () {
    test('loads the authenticated account overview', () async {
      when(() => accountApi.me()).thenAnswer((_) async => _account());

      final result = await repository.loadAccount();

      expect(result.id, 'u1');
      expect(result.email, 'ada@example.org');
    });

    test('a 403 maps to a forbidden exception', () async {
      when(() => accountApi.me()).thenThrow(_http(403, path: '/api/v1/me'));

      expect(
        () => repository.loadAccount(),
        throwsA(isA<ForbiddenException>()),
      );
    });

    test('a 404 maps to a not-found exception', () async {
      when(() => accountApi.me()).thenThrow(_http(404, path: '/api/v1/me'));

      expect(
        () => repository.loadAccount(),
        throwsA(isA<NotFoundException>()),
      );
    });
  });

  group('securityEvents', () {
    test('loads the recent security-activity events', () async {
      when(() => accountApi.securityEvents(take: any(named: 'take')))
          .thenAnswer((_) async => [
                SecurityEventDto(
                  id: 'e1',
                  eventType: 'Login.Succeeded',
                  occurredOn: DateTime(2026, 9, 7, 12),
                ),
              ]);

      final result = await repository.securityEvents();

      expect(result, hasLength(1));
      expect(result.single.eventType, 'Login.Succeeded');
      verify(() => accountApi.securityEvents(take: 100)).called(1);
    });

    test('a network failure propagates and is never an empty history',
        () async {
      when(() => accountApi.securityEvents(take: any(named: 'take'))).thenThrow(
        DioException(
          requestOptions: RequestOptions(path: '/api/v1/me/security-events'),
          type: DioExceptionType.connectionError,
        ),
      );

      expect(
        () => repository.securityEvents(),
        throwsA(isA<NetworkException>()),
      );
    });
  });

  group('changePassword', () {
    test('sends the DTO and flags the request as never-auto-retry', () async {
      when(() => accountApi.changePassword(any(), any()))
          .thenAnswer((_) async {});

      await repository.changePassword(
        currentPassword: 'old-pass',
        newPassword: 'new-pass',
      );

      final captured =
          verify(() => accountApi.changePassword(captureAny(), captureAny()))
              .captured;
      final dto = captured[0] as ChangePasswordRequestDto;
      final extra = captured[1] as Map<String, dynamic>;
      expect(dto.currentPassword, 'old-pass');
      expect(dto.newPassword, 'new-pass');
      expect(extra[AuthInterceptor.noAutoRetryKey], isTrue);
    });

    test(
        'an application 401 (no challenge header) is a wrong-current-password '
        'error, never a refresh and never a session event', () async {
      when(() => accountApi.changePassword(any(), any()))
          .thenThrow(_http(401, path: '/api/v1/me/password'));

      await expectLater(
        repository.changePassword(
          currentPassword: 'wrong',
          newPassword: 'new-pass',
        ),
        throwsA(isA<UnauthorizedException>().having(
          (e) => e.messageKey,
          'messageKey',
          'accountCurrentPasswordIncorrect',
        )),
      );
      verifyNever(() => coordinator.refreshTokens());
    });

    test(
        'a JwtBearer 401 with a recoverable token surfaces a retryable '
        'credential error', () async {
      when(() => accountApi.changePassword(any(), any())).thenThrow(_jwt401());
      when(() => coordinator.refreshTokens()).thenAnswer((_) async => TokenPair(
            accessToken: 'rotated',
            refreshToken: 'rotated-r',
            expiresAt: DateTime(2031),
          ));

      await expectLater(
        repository.changePassword(
          currentPassword: 'old-pass',
          newPassword: 'new-pass',
        ),
        throwsA(
          isA<UnauthorizedException>().having(
              (e) => e.messageKey, 'messageKey', 'accountPasswordRetryable'),
        ),
      );
      verify(() => coordinator.refreshTokens()).called(1);
    });

    test(
        'a JwtBearer 401 with a dead token maps through the centralized '
        'session-expiry path', () async {
      when(() => accountApi.changePassword(any(), any())).thenThrow(_jwt401());
      when(() => coordinator.refreshTokens()).thenAnswer((_) async => null);

      await expectLater(
        repository.changePassword(
          currentPassword: 'old-pass',
          newPassword: 'new-pass',
        ),
        throwsA(
          isA<UnauthorizedException>()
              .having((e) => e.messageKey, 'messageKey', isNull),
        ),
      );
      verify(() => coordinator.refreshTokens()).called(1);
    });

    test('a validator 400 surfaces per-field server messages lowercased',
        () async {
      when(() => accountApi.changePassword(any(), any()))
          .thenThrow(_validation400());

      var caught = false;
      try {
        await repository.changePassword(
          currentPassword: '',
          newPassword: 'short',
        );
      } on PasswordValidationException catch (error) {
        caught = true;
        expect(error.fieldErrors['currentpassword'],
            'The current password is required.');
        expect(error.fieldErrors['newpassword'],
            'The new password must be at least 12 characters long.');
        expect(error.messageKey, 'accountPasswordValidationFailed');
      }
      expect(caught, isTrue);
      verifyNever(() => coordinator.refreshTokens());
    });

    test('a 500 maps to a server failure', () async {
      when(() => accountApi.changePassword(any(), any()))
          .thenThrow(_http(500, path: '/api/v1/me/password'));

      await expectLater(
        repository.changePassword(
          currentPassword: 'old-pass',
          newPassword: 'new-pass',
        ),
        throwsA(isA<ServerException>()),
      );
      verifyNever(() => coordinator.refreshTokens());
    });
  });

  group('revokeSession', () {
    test('sends only the session id and flags never-auto-retry', () async {
      when(() => accountApi.revokeSession(any(), any()))
          .thenAnswer((_) async {});

      await repository.revokeSession('s1');

      final captured =
          verify(() => accountApi.revokeSession(captureAny(), captureAny()))
              .captured;
      expect(captured[0], 's1');
      final extra = captured[1] as Map<String, dynamic>;
      expect(extra[AuthInterceptor.noAutoRetryKey], isTrue);
    });

    test('a uniform 404 maps to NotFoundException and never refreshes',
        () async {
      when(() => accountApi.revokeSession(any(), any()))
          .thenThrow(_http(404, path: '/api/v1/me/sessions/s1/revoke'));

      await expectLater(
        repository.revokeSession('s1'),
        throwsA(isA<NotFoundException>()),
      );
      verifyNever(() => coordinator.refreshTokens());
    });

    test('a 401 maps to UnauthorizedException and is never auto-retried',
        () async {
      when(() => accountApi.revokeSession(any(), any()))
          .thenThrow(_http(401, path: '/api/v1/me/sessions/s1/revoke'));

      await expectLater(
        repository.revokeSession('s1'),
        throwsA(isA<UnauthorizedException>()),
      );
      verifyNever(() => coordinator.refreshTokens());
    });

    test('a network failure maps to NetworkException', () async {
      when(() => accountApi.revokeSession(any(), any())).thenThrow(
        DioException(
          requestOptions: RequestOptions(path: '/api/v1/me/sessions/s1/revoke'),
          type: DioExceptionType.connectionError,
        ),
      );

      await expectLater(
        repository.revokeSession('s1'),
        throwsA(isA<NetworkException>()),
      );
      verifyNever(() => coordinator.refreshTokens());
    });
  });
}
