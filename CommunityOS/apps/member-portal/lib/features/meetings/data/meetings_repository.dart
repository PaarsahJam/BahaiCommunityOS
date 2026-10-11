import 'package:dio/dio.dart';
import 'package:injectable/injectable.dart';

import '../../../core/error/app_exception.dart';
import '../../../core/network/error_mapper.dart';
import '../data/meetings_api.dart';
import '../domain/meetings_models.dart';

/// Community member-facing meetings repository.
///
/// Orchestrates authenticated access to the Meetings API. The backend remains
/// authoritative for all data and authorization; this client only maps outcomes
/// into typed results and never re-authorizes locally. Every failure is mapped
/// to a typed application exception by the shared error mapper and is never
/// conflated with a successful empty result.
@LazySingleton()
class MeetingsRepository {
  MeetingsRepository(this._api, this._mapper);

  final MeetingsApi _api;
  final ErrorMapper _mapper;

  /// Loads the list of available meetings for the authenticated member,
  /// optionally narrowed by the verified backend filters.
  Future<MeetingsListResult> listMeetings({
    DateTime? from,
    DateTime? to,
    String? organizationUnitId,
  }) async {
    try {
      final meetings = await _guard(
        () => _api.listMeetings(
          from: from,
          to: to,
          organizationUnitId: organizationUnitId,
        ),
      );
      return MeetingsListResult.success(meetings);
    } on UnauthorizedException {
      return const MeetingsListResult.unauthenticated();
    } on ForbiddenException {
      return const MeetingsListResult.forbidden();
    } on AppException catch (error) {
      return MeetingsListResult.failed(error);
    }
  }

  /// Loads a single meeting's details by its identifier.
  Future<MeetingsDetailResult> getMeetingById(String id) async {
    try {
      final dto = await _guard(() => _api.getMeeting(id: id));
      return MeetingsDetailResult.success(dto);
    } on UnauthorizedException {
      return const MeetingsDetailResult.unauthenticated();
    } on ForbiddenException {
      return const MeetingsDetailResult.forbidden();
    } on NotFoundException {
      return const MeetingsDetailResult.notFound();
    } on AppException catch (error) {
      return MeetingsDetailResult.failed(error);
    }
  }

  /// Guard method mapping Dio errors to [AppException]s via the shared
  /// [ErrorMapper], mirroring the convention used by the other repositories.
  Future<T> _guard<T>(Future<T> Function() action) async {
    try {
      return await action();
    } on DioException catch (error) {
      throw _mapper.map(error);
    }
  }
}