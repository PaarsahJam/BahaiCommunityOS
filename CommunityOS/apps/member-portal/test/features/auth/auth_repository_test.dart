import 'package:dio/dio.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:member_portal/core/network/error_mapper.dart';
import 'package:member_portal/core/storage/token_storage.dart';
import 'package:member_portal/features/auth/data/auth_api.dart';
import 'package:member_portal/features/auth/data/auth_dtos.dart';
import 'package:member_portal/features/auth/domain/auth_models.dart';
import 'package:member_portal/features/auth/domain/auth_repository.dart';
import 'package:mocktail/mocktail.dart';

class _MockAuthApi extends Mock implements AuthApi {}

class _MockAccountApi extends Mock implements AccountApi {}

class _MemoryStorage implements TokenStorage {
  TokenPair? value;

  @override
  Future<TokenPair?> read() async => value;

  @override
  Future<void> write(TokenPair tokens) async {
    value = tokens;
  }

  @override
  Future<void> clear() async {
    value = null;
  }
}

DioException _dio401() => DioException(
      requestOptions: RequestOptions(path: '/auth/login'),
      type: DioExceptionType.badResponse,
      response: Response<dynamic>(
        requestOptions: RequestOptions(path: '/auth/login'),
        statusCode: 401,
        data: {'detail': 'Invalid credentials'},
      ),
    );

void main() {
  late _MockAuthApi authApi;
  late _MockAccountApi accountApi;
  late _MemoryStorage storage;
  late AuthRepository repository;

  setUpAll(() {
    registerFallbackValue(
        const LoginRequestDto(email: 'fallback@example.org', password: 'x'));
    registerFallbackValue(const LogoutRequestDto(refreshToken: 'fallback-refresh'));
  });

  setUp(() {
    authApi = _MockAuthApi();
    accountApi = _MockAccountApi();
    storage = _MemoryStorage();
    repository =
        AuthRepository(authApi, accountApi, storage, const ErrorMapper());
  });

  group('AuthRepository', () {
    test('persists the issued token pair on a successful login', () async {
      when(() => authApi.login(any())).thenAnswer((_) async => LoginResponseDto(
            userAccountId: 'u1',
            email: 'ada@example.org',
            requiresMfa: false,
            tokens: TokenDto(
              accessToken: 'access-1',
              refreshToken: 'refresh-1',
              expiresAt: DateTime(2030),
            ),
          ));

      final result =
          await repository.login(email: 'ada@example.org', password: 'hunter2');

      expect(result, isA<AuthSignedIn>());
      final user = (result as AuthSignedIn).user;
      expect(user.userAccountId, 'u1');
      expect(user.email, 'ada@example.org');
      expect(storage.value!.accessToken, 'access-1');
      expect(storage.value!.refreshToken, 'refresh-1');
      expect(storage.value!.expiresAt, DateTime(2030));
      final captured = verify(() => authApi.login(captureAny()))
          .captured
          .cast<LoginRequestDto>();
      expect(captured.single.email, 'ada@example.org');
    });

    test('requests MFA without persisting tokens, then completes the flow',
        () async {
      when(() => authApi.login(any())).thenAnswer((inv) async {
        final request = inv.positionalArguments[0] as LoginRequestDto;
        if (request.mfaCode == null) {
          return const LoginResponseDto(
            userAccountId: 'u1',
            email: 'ada@example.org',
            requiresMfa: true,
          );
        }
        return LoginResponseDto(
          userAccountId: 'u1',
          email: 'ada@example.org',
          requiresMfa: false,
          tokens: TokenDto(
            accessToken: 'access-2',
            refreshToken: 'refresh-2',
            expiresAt: DateTime(2030),
          ),
        );
      });

      final first =
          await repository.login(email: 'ada@example.org', password: 'hunter2');

      expect(first, isA<AuthRequiresMfa>());
      expect(storage.value, isNull);

      final second = await repository.resolveMfa(
          email: 'ada@example.org', mfaCode: '123456');

      expect(second, isA<AuthSignedIn>());
      expect(storage.value!.accessToken, 'access-2');
    });

    test('rejects resolveMfa when no sign-in session is in progress', () async {
      await expectLater(
        repository.resolveMfa(email: 'a@b.c', mfaCode: '123456'),
        throwsA(
            isA<AppException>().having((e) => e.statusCode, 'statusCode', 400)),
      );
    });

    test('maps a 401 login failure to a localized invalid-credentials error',
        () async {
      when(() => authApi.login(any())).thenThrow(_dio401());

      await expectLater(
        repository.login(email: 'ada@example.org', password: 'wrong'),
        throwsA(isA<UnauthorizedException>().having(
            (e) => e.messageKey, 'messageKey', 'login_invalidCredentials')),
      );
    });

    test('maps a 401 MFA attempt to a localized invalid-code error', () async {
      when(() => authApi.login(any())).thenAnswer((inv) async {
        final request = inv.positionalArguments[0] as LoginRequestDto;
        if (request.mfaCode == null) {
          return const LoginResponseDto(
            userAccountId: 'u1',
            email: 'ada@example.org',
            requiresMfa: true,
          );
        }
        throw _dio401();
      });

      await repository.login(email: 'ada@example.org', password: 'hunter2');
      await expectLater(
        repository.resolveMfa(email: 'ada@example.org', mfaCode: '000000'),
        throwsA(isA<UnauthorizedException>()
            .having((e) => e.messageKey, 'messageKey', 'mfa_invalidCode')),
      );
    });

    test('restores the stored session pair', () async {
      storage.value = TokenPair(
        accessToken: 'a',
        refreshToken: 'r',
        expiresAt: _someTime,
      );
      final restored = await repository.restoreSession();
      expect(restored!.accessToken, 'a');
    });

    test('currentUser maps the account response to an AuthUser', () async {
      when(() => accountApi.me()).thenAnswer((_) async => UserAccountDto(
            id: 'u1',
            email: 'ada@example.org',
            status: 'Active',
            createdOn: DateTime(2020, 1, 1),
          ));

      final user = await repository.currentUser();
      expect(user.userAccountId, 'u1');
      expect(user.status, 'Active');
    });

    test('currentUser surfaces an UnauthorizedException on a 401', () async {
      when(() => accountApi.me()).thenThrow(_dio401());
      expect(
        () => repository.currentUser(),
        throwsA(isA<UnauthorizedException>()),
      );
    });

    test('logout revokes the refresh token then clears local credentials',
        () async {
      storage.value = TokenPair(
        accessToken: 'a',
        refreshToken: 'refresh-9',
        expiresAt: _someTime,
      );
      when(() => accountApi.logout(any()))
          .thenAnswer((_) async => Future<void>.value());

      await repository.logout();

      final captured = verify(() => accountApi.logout(captureAny()))
          .captured
          .cast<LogoutRequestDto>();
      expect(captured.single.refreshToken, 'refresh-9');
      expect(storage.value, isNull);
    });

    test('logout still clears local credentials when revocation fails',
        () async {
      storage.value = TokenPair(
        accessToken: 'a',
        refreshToken: 'refresh-9',
        expiresAt: _someTime,
      );
      when(() => accountApi.logout(any())).thenThrow(
          DioException(requestOptions: RequestOptions(path: '/logout')));

      await repository.logout();

      expect(storage.value, isNull);
    });

    test('logout clears the in-memory MFA password', () async {
      when(() => authApi.login(any()))
          .thenAnswer((_) async => const LoginResponseDto(
                userAccountId: 'u1',
                email: 'ada@example.org',
                requiresMfa: true,
              ));
      await repository.login(email: 'ada@example.org', password: 'hunter2');

      await repository.logout();

      await expectLater(
        repository.resolveMfa(email: 'ada@example.org', mfaCode: '123456'),
        throwsA(isA<AppException>()),
        reason: 'MFA password must not survive a logout',
      );
    });
  });
}

final _someTime = DateTime(2032, 6, 1);
