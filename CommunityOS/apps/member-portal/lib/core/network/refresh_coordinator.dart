import 'dart:async';

import 'package:dio/dio.dart';
import 'package:injectable/injectable.dart';

import '../../features/auth/data/auth_api.dart';
import '../../features/auth/data/auth_dtos.dart';
import '../storage/token_storage.dart';

/// Emitted when a session can no longer be refreshed and stored credentials
/// must be discarded.
class SessionExpiredEvent {
  const SessionExpiredEvent();
}

/// Coordinates refresh-token rotation for all authenticated API traffic.
///
/// Concurrent 401 handlers share a single in-flight refresh, so the refresh
/// endpoint is never called more than once per outage. On failure the stored
/// credentials are cleared and a [SessionExpiredEvent] is broadcast so the
/// authentication layer can transition to the unauthenticated state.
@LazySingleton()
class RefreshCoordinator {
  RefreshCoordinator(this._authApi, this._tokenStorage);

  final AuthApi _authApi;
  final TokenStorage _tokenStorage;

  Future<TokenPair?>? _inFlight;

  final StreamController<SessionExpiredEvent> _events =
      StreamController<SessionExpiredEvent>.broadcast(sync: true);

  Stream<SessionExpiredEvent> get onSessionExpired => _events.stream;

  Future<TokenPair?> refreshTokens() {
    final inFlight = _inFlight;
    if (inFlight != null) return inFlight;

    final operation = _refresh().whenComplete(() => _inFlight = null);
    _inFlight = operation;
    return operation;
  }

  Future<TokenPair?> _refresh() async {
    final tokens = await _tokenStorage.read();
    if (tokens == null) return null;

    // Snapshot the storage generation before the network round trip so a
    // logout/session-expiry clear that lands in the meantime can be detected
    // and the rotated pair discarded instead of resurrecting the session.
    final generation = await _tokenStorage.generation();

    final TokenDto response;
    try {
      response = await _authApi.refresh(
        RefreshRequestDto(refreshToken: tokens.refreshToken),
      );
    } on DioException {
      await _onRefreshFailure();
      return null;
    } catch (_) {
      await _onRefreshFailure();
      return null;
    }

    // A successful refresh also means the refresh token was rotated,
    // so the whole pair must be replaced.
    final rotated = TokenPair(
      accessToken: response.accessToken,
      refreshToken: response.refreshToken,
      expiresAt: response.expiresAt,
    );
    final persisted = await _tokenStorage.write(
      rotated,
      expectedGeneration: generation,
    );
    if (!persisted) return null;
    return rotated;
  }

  Future<void> _onRefreshFailure() async {
    await _tokenStorage.clear();
    _events.add(const SessionExpiredEvent());
  }
}
