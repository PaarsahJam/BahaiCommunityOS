import 'dart:typed_data';

import 'package:dio/dio.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:member_portal/core/network/auth_interceptor.dart';
import 'package:member_portal/core/network/error_mapper.dart';
import 'package:member_portal/core/network/refresh_coordinator.dart';
import 'package:member_portal/core/storage/token_storage.dart';
import 'package:member_portal/features/auth/data/auth_api.dart';
import 'package:member_portal/features/member/data/member_api.dart';
import 'package:member_portal/features/member/domain/member_models.dart';
import 'package:member_portal/features/member/domain/member_repository.dart';

/// In-memory [TokenStorage] with optimistic-concurrency detection, mirroring
/// the production storage contract.
class _MemoryStorage implements TokenStorage {
  TokenPair? value;
  int _generation = 0;

  @override
  Future<int> generation() async => _generation;

  @override
  Future<TokenPair?> read() async => value;

  @override
  Future<bool> write(TokenPair tokens, {int? expectedGeneration}) async {
    if (expectedGeneration != null && expectedGeneration != _generation) {
      return false;
    }
    value = tokens;
    return true;
  }

  @override
  Future<void> clear() async {
    _generation++;
    value = null;
  }
}

/// Serves deterministic per-path responses shared by the bare and authorized
/// clients, so the refresh+retry chain can be exercised end to end.
class _ScriptedAdapter implements HttpClientAdapter {
  final Map<String, ResponseBody> _responses = {};
  final Map<String, int> _calls = {};

  int callsFor(String marker) => _calls[marker] ?? 0;

  void on(String marker, int status, [String body = '{}']) {
    _responses[marker] = ResponseBody.fromString(
      body,
      status,
      headers: {
        Headers.contentTypeHeader: [Headers.jsonContentType]
      },
    );
  }

  @override
  Future<ResponseBody> fetch(
    RequestOptions options,
    Stream<Uint8List>? requestStream,
    Future<void>? cancelFuture,
  ) async {
    final uri = Uri.parse(options.baseUrl).resolve(options.path);
    final String marker;
    if (uri.path.contains('my-person')) {
      marker = 'my-person';
    } else if (uri.path.contains('refresh')) {
      marker = 'refresh';
    } else if (uri.path.contains('me')) {
      marker = 'me';
    } else if (uri.path.contains('logout')) {
      marker = 'logout';
    } else {
      marker = 'other';
    }
    _calls[marker] = (_calls[marker] ?? 0) + 1;
    return _responses[marker] ??
        ResponseBody.fromString(
          '{"detail":"not found"}',
          404,
          headers: {
            Headers.contentTypeHeader: [Headers.jsonContentType]
          },
        );
  }

  @override
  void close({bool force = false}) {}
}

BaseOptions _baseOptions() => BaseOptions(
      baseUrl: 'http://api.test/',
      responseType: ResponseType.json,
      contentType: Headers.jsonContentType,
    );

const _userAccountJson =
    '{"id":"u1","email":"ada@example.org","status":"Active",'
    '"createdOn":"2020-01-01T00:00:00.000Z"}';

TokenPair _pair() => TokenPair(
      accessToken: 'access-1',
      refreshToken: 'refresh-1',
      expiresAt: DateTime(2030),
    );

/// Real dio over a scripted adapter, with the same network wiring as
/// production: a bare client for auth endpoints and an authorized client
/// whose interceptor performs exactly one refresh+retry on 401.
///
/// These are plain (non-widget) tests on purpose: the refresh/retry chain is
/// driven by real microtask/timer scheduling that the widget-test fake-async
/// zone distorts. The widget-level routing consequences (returns to sign-in,
/// the shell keeps rendering) are covered by the auth/member widget suites.
void main() {
  test(
      'a 401 on the member bootstrap triggers one refresh; a failed refresh '
      'clears credentials and signals session expiry', () async {
    final adapter = _ScriptedAdapter()
      ..on('me', 200, _userAccountJson)
      ..on('my-person', 401, '{"detail":"token expired"}')
      ..on('refresh', 401, '{"detail":"refresh token invalid"}');

    final storage = _MemoryStorage()..value = _pair();

    // Bare client: auth endpoints only, no interceptors, so a refresh can
    // never recurse through the interceptor.
    final bare = Dio(_baseOptions())..httpClientAdapter = adapter;
    final coordinator = RefreshCoordinator(AuthApi(bare), storage);
    var expiryEvents = 0;
    final sub = coordinator.onSessionExpired.listen((_) => expiryEvents++);

    // Authorized client: bearer header + one automatic refresh/retry.
    final authorized = Dio(_baseOptions())..httpClientAdapter = adapter;
    authorized.interceptors.add(
      AuthInterceptor(storage, coordinator)..retryClient = authorized,
    );

    const mapper = ErrorMapper();
    final memberRepo = MemberRepository(
      AccountApi(authorized),
      MemberApi(authorized),
      mapper,
    );

    // The refresh failed: the coordinator has already discarded the stored
    // credentials, the expiry event fired, and the bootstrap resolves as an
    // unauthenticated outcome so the auth layer can route back to sign-in.
    final result = await memberRepo.loadMemberSession();
    await sub.cancel();

    expect(result, isA<MemberSessionUnauthenticated>());
    expect(expiryEvents, 1);
    expect(storage.value, isNull);

    // Exactly one 401 attempt on the member endpoint and exactly one refresh:
    // no loop, no zombie retry, no duplicate refresh.
    expect(adapter.callsFor('my-person'), 1);
    expect(adapter.callsFor('refresh'), 1);
  });

  test(
      'a 401 that recovers via a successful refresh rotates the pair and '
      'executes the retried request', () async {
    final adapter = _ScriptedAdapter()
      ..on('me', 200, _userAccountJson)
      ..on('my-person', 401, '{"detail":"token expired"}')
      ..on(
          'refresh',
          200,
          '{"accessToken":"rotated-access",'
              '"refreshToken":"rotated-refresh","expiresAt":"2031-01-01T00:00:00.000Z"}');

    final storage = _MemoryStorage()..value = _pair();
    final bare = Dio(_baseOptions())..httpClientAdapter = adapter;
    final coordinator = RefreshCoordinator(AuthApi(bare), storage);
    var expiryEvents = 0;
    final sub = coordinator.onSessionExpired.listen((_) => expiryEvents++);
    final authorized = Dio(_baseOptions())..httpClientAdapter = adapter;
    authorized.interceptors.add(
      AuthInterceptor(storage, coordinator)..retryClient = authorized,
    );

    const mapper = ErrorMapper();
    final memberRepo = MemberRepository(
      AccountApi(authorized),
      MemberApi(authorized),
      mapper,
    );

    // /my-person is still scripted as 401 after rotation, so the bootstrap
    // cannot succeed here; what this case pins down is the recovery wiring:
    // the refresh ran exactly once, the rotated pair was persisted, and the
    // retried request was the *second* (marked) attempt on /my-person. A
    // recovered token never signals session expiry.
    await memberRepo.loadMemberSession().timeout(const Duration(seconds: 10));
    await sub.cancel();

    expect(expiryEvents, 0);
    expect(storage.value!.accessToken, 'rotated-access');
    expect(storage.value!.refreshToken, 'rotated-refresh');
    expect(adapter.callsFor('my-person'), 2);
    expect(adapter.callsFor('refresh'), 1);
  });
}
