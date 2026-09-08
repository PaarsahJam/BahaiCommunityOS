import 'package:dio/dio.dart';
import 'package:retrofit/retrofit.dart';

import 'auth_dtos.dart';

part 'auth_api.g.dart';

/// Endpoints reachable without authentication. Backed by the bare client so
/// the auth interceptor can never interfere with (or recurse through) them.
@RestApi()
abstract class AuthApi {
  factory AuthApi(Dio dio, {String baseUrl}) = _AuthApi;

  @POST('auth/login')
  Future<LoginResponseDto> login(@Body() LoginRequestDto request);

  @POST('auth/refresh')
  Future<TokenDto> refresh(@Body() RefreshRequestDto request);
}

/// Endpoints guarded by the caller's access token. Backed by the authorized
/// client (bearer header + transparent refresh/retry).
@RestApi()
abstract class AccountApi {
  factory AccountApi(Dio dio, {String baseUrl}) = _AccountApi;

  @GET('me')
  Future<UserAccountDto> me();

  @GET('me/security-events')
  Future<List<SecurityEventDto>> securityEvents(
      {@Query('take') int take = 100});

  @GET('me/sessions')
  Future<List<SessionDto>> sessions();

  /// Begins TOTP enrollment for the caller and returns the one-time
  /// enrollment material ([MfaEnrollmentDto.secret] and `.provisioningUri`)
  /// that must never leave transient page state.
  @POST('mfa/enroll')
  Future<MfaEnrollmentDto> beginMfaEnrollment();

  /// Completes TOTP enrollment for the enrollment returned by the caller's own
  /// `POST /mfa/enroll` operation. The backend is authoritative; the
  /// completion resolves the method by id and is consumed as-is by Phase 1.
  @POST('mfa/enroll/complete')
  Future<void> completeMfaEnrollment(
      @Body() CompleteMfaEnrollmentRequestDto request);

  /// Changes the caller's password.
  ///
  /// The backend responds with `204` and revokes the *entire* token family
  /// (`RevokeAllForUserAsync`), so every refresh token is invalidated from
  /// here. This is a non-idempotent mutation: it is the one authenticated
  /// endpoint that must *never* be transparently refresh+retried. The caller
  /// passes `extra: {AuthInterceptor.noAutoRetryKey: true}` so a 401 is
  /// surfaced verbatim for the repository to discriminate.
  @POST('me/password')
  Future<void> changePassword(
    @Body() ChangePasswordRequestDto request,
    @Extras() Map<String, dynamic>? extra,
  );

  /// Revokes one session owned by the caller (`POST /me/sessions/{id}/revoke`).
  ///
  /// Non-idempotent in effect (it revokes server-side state): it must *never*
  /// be transparently refresh+retried. The caller passes
  /// `extra: {AuthInterceptor.noAutoRetryKey: true}` so a 401 is surfaced
  /// verbatim for the upstream layer to discriminate.
  @POST('me/sessions/{sessionId}/revoke')
  Future<void> revokeSession(
    @Path('sessionId') String sessionId,
    @Extras() Map<String, dynamic>? extra,
  );

  @POST('account/logout')
  Future<void> logout(@Body() LogoutRequestDto request);
}
