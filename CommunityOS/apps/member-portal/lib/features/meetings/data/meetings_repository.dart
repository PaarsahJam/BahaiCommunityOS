import 'package:dio/dio.dart';
import 'package:injectable/injectable.dart';

import '../../../core/network/auth_interceptor.dart';
import '../../../core/network/error_mapper.dart';
import '../../../core/network/refresh_coordinator.dart';
import '../../account/domain/account_exceptions.dart';
import '../../auth/data/auth_api.dart';
import '../../auth/data/auth_dtos.dart';
import '../../auth/domain/auth_models.dart';
import '../data/meetings_api.dart';
import '../data/meetings_dtos.dart';
import '../domain/meetings_models.dart';

/// Community member-facing meetings repository.
///
/// Orchestrates authenticated access to the Meetings API. The backend remains
/// authoritative for all data and authorization; this client only maps outcomes
/// into typed results and never re-authorizes locally.
@LazySingleton()
class MeetingsRepository {
  MeetingsRepository(
    this._api,
    this._mapper,
    this._coordinator,
  );

  final MeetingsApi _api;
  final ErrorMapper _mapper;
  final RefreshCoordinator _coordinator;

  /// Loads the list of available meetings for the authenticated member.
  ///
  /// Error semantics:
  /// * 401 from any call propagates as an unauthenticated/expired-session
  ///   outcome;
  /// * 403 propagates as forbidden;
  /// * network, timeout and server failures propagate as a failed outcome.
  Future<Either<AppException, List<MeetingDto>>> listMeetings({
    DateTime? from,
    DateTime? to,
    String? organizationUnitId,
  }) async {
    try {
      final dto = await _api.listMeetings(
        from: from,
        to: to,
        organizationUnitId: organizationUnitId,
      );
      return right(dto.meetings);
    } on UnauthorizedException {
      return left(const UnauthorizedException('Unauthenticated'));
    } on ForbiddenException {
      return left(const ForbiddenException('Forbidden'));
    } on AppException catch (error) {
      return left(error);
    } catch (error) {
      return left(
        const RequestTimeoutException('The request timed out. Please try again.'),
      );
    }
  }

  /// Loads a meeting's details by its ID.
  ///
  /// Error semantics:
  /// * 401 from any call propagates as an unauthenticated/expired-session
  ///   outcome;
  /// * 404 propagates as not found;
  /// * 403 propagates as forbidden;
  /// * network, timeout and server failures propagate as a failed outcome.
  Future<Either<AppException, MeetingDto>> getMeetingById(String id) async {
    try {
      final dto = await _api.getMeeting(id: id);
      return right(dto);
    } on UnauthorizedException {
      return left(const UnauthorizedException('Unauthenticated'));
    } on NotFoundException {
      return left(const NotFoundException('Meeting not found'));
    } on ForbiddenException {
      return left(const ForbiddenException('Forbidden'));
    } on AppException catch (error) {
      return left(error);
    } catch (error) {
      return left(
        const RequestTimeoutException('The request timed out. Please try again.'),
      );
    }
  }

  /// Guard method mapping Dio errors to AppExceptions.
  ///
  /// Mirrors the pattern used in [MemberRepository._guard].
  Future<T> _guard<T>(Future<T> Function() action) async {
    try {
      return await action();
    } on DioException catch (error) {
      throw _mapper.map(error);
    }
  }
}