import 'package:dio/dio.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:member_portal/core/network/auth_interceptor.dart';
import 'package:member_portal/core/network/refresh_coordinator.dart';
import 'package:member_portal/core/storage/token_storage.dart';
import 'package:mocktail/mocktail.dart';

class _MockStorage extends Mock implements TokenStorage {}

class _MockCoordinator extends Mock implements RefreshCoordinator {}

class _MockAdapter extends Mock implements HttpClientAdapter {}

void main() {
  late _MockStorage storage;
  late _MockCoordinator coordinator;
  late Dio dio;
  late _MockAdapter adapter;

  setUpAll(() {
    registerFallbackValue(RequestOptions(path: 'fallback'));
  });

  setUp(() {
    storage = _MockStorage();
    coordinator = _MockCoordinator();
    adapter = _MockAdapter();
    dio = Dio(BaseOptions(baseUrl: 'http://api.test'));
    dio.httpClientAdapter = adapter;
    dio.interceptors
        .add(AuthInterceptor(storage, coordinator)..retryClient = dio);
  });

  ResponseBody json(int status, [String body = '']) => ResponseBody.fromString(
        body,
        status,
        headers: {
          Headers.contentTypeHeader: [Headers.jsonContentType]
        },
      );

  group('AuthInterceptor', () {
    test('attaches the stored access token to an authenticated request',
        () async {
      when(() => storage.read()).thenAnswer((_) async => TokenPair(
            accessToken: 'access-123',
            refreshToken: 'refresh-123',
            expiresAt: DateTime(2040),
          ));
      when(() => adapter.fetch(any(), any(), any())).thenAnswer(
        (_) async => json(200, '{"id":"1"}'),
      );

      await dio.get('/me');

      final captured =
          verify(() => adapter.fetch(captureAny(), any(), any())).captured;
      final options = captured.first as RequestOptions;
      expect(options.headers['Authorization'], 'Bearer access-123');
    });

    test('never touches anonymous endpoints even when tokens exist', () async {
      when(() => storage.read()).thenAnswer((_) async => TokenPair(
            accessToken: 'access-123',
            refreshToken: 'refresh-123',
            expiresAt: DateTime(2040),
          ));
      when(() => adapter.fetch(any(), any(), any())).thenAnswer(
        (_) async => json(200, '{"accessToken":"new"}'),
      );

      await dio.post('/auth/login', data: {'email': 'a@b.c'});

      final captured =
          verify(() => adapter.fetch(captureAny(), any(), any())).captured;
      final options = captured.first as RequestOptions;
      expect(options.headers.containsKey('Authorization'), isFalse);
      verifyNever(() => coordinator.refreshTokens());
    });

    test('retries exactly once with a refreshed token after a 401', () async {
      var refreshed = false;
      when(() => storage.read()).thenAnswer((_) async => TokenPair(
            accessToken: refreshed ? 'fresh-access' : 'stale-access',
            refreshToken: refreshed ? 'fresh-refresh' : 'stale-refresh',
            expiresAt: DateTime(2040),
          ));
      when(() => coordinator.refreshTokens()).thenAnswer((_) async {
        refreshed = true;
        return TokenPair(
          accessToken: 'fresh-access',
          refreshToken: 'fresh-refresh',
          expiresAt: DateTime(2041),
        );
      });

      var requests = 0;
      final headers = <String?>[];
      when(() => adapter.fetch(any(), any(), any())).thenAnswer((inv) async {
        requests++;
        final options = inv.positionalArguments[0] as RequestOptions;
        headers.add(options.headers['Authorization']);
        if (options.path == '/me' && requests == 1) {
          return json(401);
        }
        return json(200, '{"id":"1"}');
      });

      final response = await dio.get('/me');

      expect(response.statusCode, 200);
      expect(requests, 2);
      verify(() => coordinator.refreshTokens()).called(1);
      expect(headers, ['Bearer stale-access', 'Bearer fresh-access']);
    });

    test('passes the original 401 through when refresh fails', () async {
      when(() => storage.read()).thenAnswer((_) async => TokenPair(
            accessToken: 'stale-access',
            refreshToken: 'stale-refresh',
            expiresAt: DateTime(2040),
          ));
      when(() => coordinator.refreshTokens()).thenAnswer((_) async => null);
      when(() => adapter.fetch(any(), any(), any()))
          .thenAnswer((_) async => json(401));

      await expectLater(
        dio.get('/me'),
        throwsA(isA<DioException>()
            .having((e) => e.response?.statusCode, 'status', 401)),
      );

      verify(() => adapter.fetch(any(), any(), any())).called(1);
      verify(() => coordinator.refreshTokens()).called(1);
    });

    test('never refreshes for anonymous endpoints even on 401', () async {
      when(() => storage.read()).thenAnswer((_) async => null);
      when(() => adapter.fetch(any(), any(), any()))
          .thenAnswer((_) async => json(401));

      await expectLater(
        dio.post('/auth/login', data: {'email': 'a@b.c'}),
        throwsA(isA<DioException>()
            .having((e) => e.response?.statusCode, 'status', 401)),
      );

      verifyNever(() => coordinator.refreshTokens());
    });
  });
}
