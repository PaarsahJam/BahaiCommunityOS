import 'package:dio/dio.dart';
import 'package:injectable/injectable.dart';

import '../../../core/network/auth_interceptor.dart';
import '../../../core/network/error_mapper.dart';
import '../../../core/network/refresh_coordinator.dart';
import '../../account/domain/account_exceptions.dart';
import '../../auth/data/auth_api.dart';
import '../../auth/data/auth_dtos.dart';
import '../../auth/domain/auth_models.dart';
import '../data/member_api.dart';
import '../data/member_dtos.dart';
import 'member_models.dart';

/// Community member-facing repository.
///
/// Orchestrates the authenticated member-context bootstrap and profile reads.
/// The backend remains authoritative for authorization; this client only maps
/// outcomes into typed results and never re-authorizes locally.
@LazySingleton()
class MemberRepository {
  MemberRepository(
    this._accountApi,
    this._api,
    this._mapper,
    this._coordinator,
  );

  final AccountApi _accountApi;
  final MemberApi _api;
  final ErrorMapper _mapper;
  final RefreshCoordinator _coordinator;

  /// Bootstraps the authenticated member context from `/me`, `/my-person` and
  /// `/memberships/by-person/{personId}`.
  ///
  /// Error semantics:
  /// * a membership *absence* is expressed by the backend as HTTP 200 + null —
  ///   it is never synthesized from a failure;
  /// * 401 from any call propagates as an unauthenticated/expired-session
  ///   outcome;
  /// * 403 propagates as forbidden;
  /// * network, timeout and server failures propagate as a failed outcome.
  Future<MemberSessionResult> loadMemberSession() async {
    final AuthUser account;
    try {
      account = AuthUser.fromUserAccount(
        await _guard(() => _accountApi.me()),
      );
    } on UnauthorizedException {
      return const MemberSessionResult.unauthenticated();
    } on ForbiddenException catch (error) {
      return MemberSessionResult.forbidden(_keyed(error));
    } on AppException catch (error) {
      return MemberSessionResult.failed(error);
    }

    final PersonDto person;
    try {
      person = await _guard(() => _api.getMyPerson());
    } on NotFoundException {
      return const MemberSessionResult.unlinked();
    } on UnauthorizedException {
      return const MemberSessionResult.unauthenticated();
    } on ForbiddenException catch (error) {
      return MemberSessionResult.forbidden(_keyed(error));
    } on AppException catch (error) {
      return MemberSessionResult.failed(error);
    }

    final MembershipDto? membership;
    try {
      membership = await _guard(() => _api.getMembershipByPerson(person.id));
    } on UnauthorizedException {
      return const MemberSessionResult.unauthenticated();
    } on ForbiddenException catch (error) {
      return MemberSessionResult.forbidden(_keyed(error));
    } on AppException catch (error) {
      return MemberSessionResult.failed(error);
    }

    return MemberSessionResult.resolved(
      account: account,
      person: person,
      membership: membership,
    );
  }

  /// Loads a person's profile detail record, subject to backend authorization
  /// and privacy masking. The caller must treat nullable fields as absences.
  Future<PersonDetailDto> personDetail(String personId) =>
      _guard(() => _api.getPerson(personId));

  /// Loads the authoritative membership record for [personId] — the member's
  /// own Community PersonId from the authenticated `MemberContext`.
  ///
  /// Error semantics mirror the backend exactly: the record on `200 + DTO`,
  /// `null` on `200 + null` (the legitimate no-membership state), and
  /// 401/403/404/timeout/network/500 all propagate as typed application
  /// exceptions — never as null.
  Future<MembershipDto?> getMembership(String personId) =>
      _guard(() => _api.getMembershipByPerson(personId));

  /// Loads the authenticated account overview record from `/me`.
  ///
  /// 401 follows the existing session-expiry refresh machinery; 403/404
  /// propagate as keyed failures; timeout/network/5xx stay retryable.
  Future<UserAccountDto> loadAccount() => _guard(() => _accountApi.me());

  /// Loads the most recent security-activity events for the authenticated
  /// account (`/me/security-events?take=100`).
  ///
  /// Read-only and self-scoped; failures are retryable and are never conflated
  /// with a successful empty history.
  Future<List<SecurityEventDto>> securityEvents({int take = 100}) =>
      _guard(() => _accountApi.securityEvents(take: take));

  /// Changes the authenticated account's password and deliberately invalidates
  /// the whole token family (the backend revokes *all* sessions).
  ///
  /// This is a non-idempotent, session-revoking mutation. Its request carries
  /// [AuthInterceptor.noAutoRetryKey] so the transparent refresh+retry can
  /// never re-submit it. The caller therefore receives a verbatim 401 whose
  /// two deterministic meanings are discriminated here:
  ///
  /// * an *application* 401 (no `WWW-Authenticate` challenge header): the
  ///   backend rejected the current password, so the mutation ran and failed.
  ///   Surfaces as a form-level credential error
  ///   (`accountCurrentPasswordIncorrect`) — never as a session event.
  /// * a *JwtBearer* 401 (challenge header present): the token was
  ///   stale/invalid. Exactly one refresh is attempted; a live session
  ///   recovers and retries manually, and a failed refresh has already
  ///   broadcast the centralized session-expiry event. The mutation itself is
  ///   never re-submitted automatically.
  Future<void> changePassword({
    required String currentPassword,
    required String newPassword,
  }) async {
    try {
      await _accountApi.changePassword(
        ChangePasswordRequestDto(
          currentPassword: currentPassword,
          newPassword: newPassword,
        ),
        {AuthInterceptor.noAutoRetryKey: true},
      );
    } on DioException catch (error) {
      final status = error.response?.statusCode;
      if (status == 401) {
        throw await _classifyPasswordZeroOne(error);
      }
      if (status == 400) {
        final fieldErrors = _fieldErrors(error.response);
        final message = fieldErrors.values.isNotEmpty
            ? fieldErrors.values.first
            : 'Your new password was rejected.';
        throw PasswordValidationException(
          message,
          fieldErrors: fieldErrors,
          messageKey: 'accountPasswordValidationFailed',
          statusCode: 400,
        );
      }
      throw _mapper.map(error);
    }
  }

  Future<AppException> _classifyPasswordZeroOne(DioException error) async {
    final challenge = error.response?.headers.value('www-authenticate') ?? '';
    if (challenge.isEmpty) {
      return const UnauthorizedException(
        'The current password is incorrect.',
        messageKey: 'accountCurrentPasswordIncorrect',
        statusCode: 401,
      );
    }
    final tokens = await _coordinator.refreshTokens();
    if (tokens != null) {
      return const UnauthorizedException(
        'Your session is being refreshed. Please try again.',
        messageKey: 'accountPasswordRetryable',
        statusCode: 401,
      );
    }
    return _mapper.map(error);
  }

  Map<String, String> _fieldErrors(Response<dynamic>? response) {
    final body = response?.data;
    if (body is! Map) return const {};
    final errors = body['errors'];
    if (errors is! List) return const {};
    final map = <String, String>{};
    for (final entry in errors) {
      if (entry is! Map) continue;
      final property = entry['propertyName'];
      final message = entry['errorMessage'];
      if (property is String &&
          property.trim().isNotEmpty &&
          message is String &&
          message.trim().isNotEmpty) {
        map[property.toLowerCase()] = message.trim();
      }
    }
    return map;
  }

  ForbiddenException _keyed(ForbiddenException error) => ForbiddenException(
        error.message,
        messageKey: 'profile_forbidden',
        code: error.code,
        statusCode: error.statusCode,
      );

  Future<T> _guard<T>(Future<T> Function() action) async {
    try {
      return await action();
    } on DioException catch (error) {
      throw _mapper.map(error);
    }
  }
}
