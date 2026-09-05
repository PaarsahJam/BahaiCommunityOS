import 'dart:async';

import 'package:dio/dio.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:member_portal/core/network/refresh_coordinator.dart';
import 'package:member_portal/core/storage/token_storage.dart';
import 'package:member_portal/features/auth/data/auth_api.dart';
import 'package:member_portal/features/auth/data/auth_dtos.dart';
import 'package:mocktail/mocktail.dart';

class _MockAuthApi extends Mock implements AuthApi {}

class _MemoryStorage implements TokenStorage {
  TokenPair? value;
  int writes = 0;

  @override
  Future<TokenPair?> read() async => value;

  @override
  Future<void> write(TokenPair tokens) async {
    value = tokens;
    writes++;
  }

  @override
  Future<void> clear() async {
    value = null;
  }
}

TokenPair _pair({String access = 'at', String refresh = 'rt'}) => TokenPair(
      accessToken: access,
      refreshToken: refresh,
      expiresAt: DateTime(2030),
    );

TokenDto _tokenDto() => TokenDto(
      accessToken: 'rotated-access',
      refreshToken: 'rotated-refresh',
      expiresAt: DateTime(2031),
    );

void main() {
  late _MockAuthApi api;
  late _MemoryStorage storage;
  late RefreshCoordinator coordinator;

  setUpAll(() {
    registerFallbackValue(
        const RefreshRequestDto(refreshToken: 'fallback-refresh-token'));
  });

  setUp(() {
    api = _MockAuthApi();
    storage = _MemoryStorage();
    coordinator = RefreshCoordinator(api, storage);
  });

  group('RefreshCoordinator', () {
    test('returns null without calling the API when nothing is stored',
        () async {
      final result = await coordinator.refreshTokens();
      expect(result, isNull);
      verifyNever(() => api.refresh(any()));
    });

    test('exchanges the stored refresh token and persists the rotated pair',
        () async {
      storage.value = _pair();
      when(() => api.refresh(any())).thenAnswer((_) async => _tokenDto());

      final result = await coordinator.refreshTokens();

      expect(result, isNotNull);
      expect(result!.accessToken, 'rotated-access');
      expect(result.refreshToken, 'rotated-refresh');
      expect(storage.value!.accessToken, 'rotated-access');
      expect(storage.value!.refreshToken, 'rotated-refresh');
      final captured = verify(() => api.refresh(captureAny()))
          .captured
          .cast<RefreshRequestDto>();
      expect(captured.single.refreshToken, 'rt');
    });

    test('deduplicates concurrent refreshes into a single API call', () async {
      storage.value = _pair();
      final completer = Completer<TokenDto>();
      when(() => api.refresh(any())).thenAnswer((_) => completer.future);

      final first = coordinator.refreshTokens();
      final second = coordinator.refreshTokens();
      completer.complete(_tokenDto());

      final resultA = await first;
      final resultB = await second;

      expect(resultA!.accessToken, 'rotated-access');
      expect(resultB!.accessToken, 'rotated-access');
      verify(() => api.refresh(any())).called(1);
      expect(storage.writes, 1);
    });

    test('clears stored credentials and broadcasts session expiry on failure',
        () async {
      storage.value = _pair();
      when(() => api.refresh(any())).thenThrow(
        DioException(requestOptions: RequestOptions(path: '/auth/refresh')),
      );

      final events = <SessionExpiredEvent>[];
      final sub = coordinator.onSessionExpired.listen(events.add);
      final result = await coordinator.refreshTokens();

      expect(result, isNull);
      expect(storage.value, isNull);
      expect(events, hasLength(1));
      await sub.cancel();
    });

    test('a later refresh recovers after a failed one', () async {
      storage.value = _pair();
      when(() => api.refresh(any()))
          .thenAnswer((_) async => throw StateError('boom'));
      expect(await coordinator.refreshTokens(), isNull);

      when(() => api.refresh(any())).thenAnswer((_) async => _tokenDto());
      storage.value = _pair();
      final result = await coordinator.refreshTokens();
      expect(result!.accessToken, 'rotated-access');
    });
  });
}
