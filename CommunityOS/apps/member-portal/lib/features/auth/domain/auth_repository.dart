import 'package:dio/dio.dart';
import 'package:injectable/injectable.dart';

import '../../../core/network/error_mapper.dart';
import '../../../core/storage/token_storage.dart';
import '../data/auth_api.dart';
import '../data/auth_dtos.dart';
import 'auth_models.dart';

/// Orchestrates credential exchange with the Identity service and persistence
/// of issued token pairs. Credentials are never logged or stored anywhere
/// except the secure token store.
@LazySingleton()
class AuthRepository {
  AuthRepository(
    this._authApi,
    this._accountApi,
    this._storage,
    this._mapper,
  );

  final AuthApi _authApi;
  final AccountApi _accountApi;
  final TokenStorage _storage;
  final ErrorMapper _mapper;

  /// Held only in memory for the brief MFA round trip; cleared on success,
  /// failure, and logout. Never persisted or logged.
  String? _pendingMfaPassword;

  Future<TokenPair?> restoreSession() => _storage.read();

  Future<AuthLoginResult> login({
    required String email,
    required String password,
  }) {
    return _login(email: email, password: password);
  }

  Future<AuthLoginResult> resolveMfa({
    required String email,
    required String mfaCode,
  }) async {
    final password = _pendingMfaPassword;
    if (password == null) {
      throw const AppException(
        'No sign-in session is in progress.',
        statusCode: 400,
      );
    }
    return _login(email: email, password: password, mfaCode: mfaCode);
  }

  Future<AuthLoginResult> _login({
    required String email,
    required String password,
    String? mfaCode,
  }) async {
    final generation = await _storage.generation();
    final LoginResponseDto response;
    try {
      response = await _authApi.login(
        LoginRequestDto(
          email: email,
          password: password,
          mfaCode: mfaCode,
        ),
      );
    } on DioException catch (error) {
      _pendingMfaPassword = null;
      throw _decorateLoginError(_mapper.map(error), mfaCode: mfaCode);
    } catch (_) {
      _pendingMfaPassword = null;
      throw const AppException('Sign-in failed. Please try again.');
    }

    if (response.requiresMfa) {
      _pendingMfaPassword = password;
      return AuthLoginResult.requiresMfa(
        accountId: response.userAccountId,
        email: email,
      );
    }

    final tokens = response.tokens;
    if (tokens == null) {
      _pendingMfaPassword = null;
      throw const AppException('Sign-in response did not include tokens.');
    }

    final persisted = await _storage.write(
      TokenPair(
        accessToken: tokens.accessToken,
        refreshToken: tokens.refreshToken,
        expiresAt: tokens.expiresAt,
      ),
      expectedGeneration: generation,
    );
    _pendingMfaPassword = null;
    if (!persisted) {
      // The credentials exchange outlived a logout/session-expiry clear;
      // persisting now would resurrect a session that was explicitly ended.
      throw const AppException(
        'Sign-in was interrupted. Please try again.',
      );
    }
    return AuthLoginResult.authenticated(
      user: AuthUser(
        userAccountId: response.userAccountId,
        email: email,
      ),
    );
  }

  AppException _decorateLoginError(AppException error, {String? mfaCode}) {
    if (error is UnauthorizedException) {
      return UnauthorizedException(
        error.message,
        messageKey:
            mfaCode == null ? 'login_invalidCredentials' : 'mfa_invalidCode',
        code: error.code,
        statusCode: error.statusCode,
      );
    }
    return error;
  }

  Future<AuthUser> currentUser() async {
    final UserAccountDto account;
    try {
      account = await _accountApi.me();
    } on DioException catch (error) {
      throw _mapper.map(error);
    }
    return AuthUser.fromUserAccount(account);
  }

  /// Revokes the refresh token server-side (best effort) and always clears
  /// local credentials.
  Future<void> logout() async {
    final tokens = await _storage.read();
    if (tokens != null) {
      try {
        await _accountApi.logout(
          LogoutRequestDto(refreshToken: tokens.refreshToken),
        );
      } on DioException {
        // Server-side revocation is best-effort; local credentials still clear.
      } catch (_) {
        // Any unexpected outcome must never block sign-out.
      }
    }
    await _storage.clear();
    _pendingMfaPassword = null;
  }

  Future<void> clearLocalAuth() async {
    await _storage.clear();
    _pendingMfaPassword = null;
  }
}
