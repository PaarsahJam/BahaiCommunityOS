import 'package:dio/dio.dart';
import 'package:injectable/injectable.dart';

import '../../../core/network/error_mapper.dart';
import '../../auth/data/auth_api.dart';
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
  );

  final AccountApi _accountApi;
  final MemberApi _api;
  final ErrorMapper _mapper;

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
