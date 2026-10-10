import 'package:dio/dio.dart';
import 'package:injectable/injectable.dart';

import '../../../core/network/error_mapper.dart';
import '../data/activities_api.dart';
import '../data/activities_dtos.dart';

/// Community member-facing activities repository.
///
/// The backend remains authoritative for all data and authorization; this
/// client only maps outcomes into typed values and never re-authorizes locally.
/// Every Dio/HTTP failure is mapped to a typed application exception by the
/// shared error mapper and is never conflated with a successful empty result.
@LazySingleton()
class ActivitiesRepository {
  ActivitiesRepository(this._api, this._mapper);

  final ActivitiesApi _api;
  final ErrorMapper _mapper;

  /// Loads the available activities for the authenticated member, optionally
  /// narrowed by the verified backend filters. A failure is retryable and is
  /// never rendered as an empty list.
  Future<List<ActivityDto>> listActivities({
    DateTime? from,
    DateTime? to,
    String? organizationUnitId,
  }) =>
      _guard(() => _api.listActivities(
            from: from,
            to: to,
            organizationUnitId: organizationUnitId,
          ));

  /// Loads one activity's details by its identifier.
  ///
  /// A 404 propagates as a not-found application exception; every other failure
  /// maps through the shared error mapper and is never conflated with a
  /// successful result.
  Future<ActivityDto> detailActivity(String id) =>
      _guard(() => _api.detailActivity(id: id));

  Future<T> _guard<T>(Future<T> Function() action) async {
    try {
      return await action();
    } on DioException catch (error) {
      throw _mapper.map(error);
    }
  }
}
