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

  @POST('account/logout')
  Future<void> logout(@Body() LogoutRequestDto request);
}
