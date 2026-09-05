import 'package:flutter_secure_storage/flutter_secure_storage.dart';
import 'package:injectable/injectable.dart';

/// A rotated token pair received from the Identity service.
class TokenPair {
  const TokenPair({
    required this.accessToken,
    required this.refreshToken,
    required this.expiresAt,
  });

  final String accessToken;
  final String refreshToken;
  final DateTime expiresAt;

  bool get isExpired => DateTime.now().isAfter(expiresAt);
}

/// Persistence contract for credentials. Implementations must never surface
/// token values to logs or non-secure storage.
abstract interface class TokenStorage {
  Future<TokenPair?> read();

  /// Persists [tokens] unless [expectedGeneration] no longer matches the
  /// storage's current generation — i.e. a logout or session-expiry clear
  /// landed while the credential exchange was in flight. Returns whether the
  /// write was applied. A disregarded (stale) write must never resurrect a
  /// discarded session.
  Future<bool> write(TokenPair tokens, {int? expectedGeneration});

  Future<void> clear();

  /// Monotonic counter that every [clear] increments. Snapshot it immediately
  /// before an asynchronous credential exchange and hand it back to [write]
  /// so a late completion cannot repopulate cleared credentials.
  Future<int> generation();
}

@LazySingleton(as: TokenStorage)
class SecureTokenStorage implements TokenStorage {
  SecureTokenStorage(this._storage);

  final FlutterSecureStorage _storage;

  int _generation = 0;

  static const _accessTokenKey = 'auth.access_token';
  static const _refreshTokenKey = 'auth.refresh_token';
  static const _expiresAtKey = 'auth.token_expires_at';

  @override
  Future<int> generation() async => _generation;

  @override
  Future<TokenPair?> read() async {
    final accessToken = await _storage.read(key: _accessTokenKey);
    final refreshToken = await _storage.read(key: _refreshTokenKey);
    final rawExpiresAt = await _storage.read(key: _expiresAtKey);
    if (accessToken == null || refreshToken == null || rawExpiresAt == null) {
      return null;
    }
    final expiresAt = DateTime.tryParse(rawExpiresAt);
    if (expiresAt == null) return null;
    return TokenPair(
      accessToken: accessToken,
      refreshToken: refreshToken,
      expiresAt: expiresAt,
    );
  }

  @override
  Future<bool> write(TokenPair tokens, {int? expectedGeneration}) async {
    if (expectedGeneration != null && expectedGeneration != _generation) {
      return false;
    }
    await _storage.write(key: _accessTokenKey, value: tokens.accessToken);
    await _storage.write(key: _refreshTokenKey, value: tokens.refreshToken);
    await _storage.write(
        key: _expiresAtKey, value: tokens.expiresAt.toIso8601String());
    return true;
  }

  @override
  Future<void> clear() async {
    _generation++;
    await _storage.delete(key: _accessTokenKey);
    await _storage.delete(key: _refreshTokenKey);
    await _storage.delete(key: _expiresAtKey);
  }
}
