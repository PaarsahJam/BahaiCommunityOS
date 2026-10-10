/// Result of a [ActivitiesRepository.listActivities] call.
sealed class ActivitiesListResult {
  const ActivitiesListResult._();

  /// Successful result with a list of activities.
  const factory ActivitiesListResult.success(
    List<ActivityDto> activities,
  ) = _ActivitiesListResultSuccess;

  /// Unauthenticated result (session expired / no token).
  const factory ActivitiesListResult.unauthenticated() =
      _ActivitiesListResultUnauthenticated;

  /// Forbidden result (403 - member lacks permission).
  const factory ActivitiesListResult.forbidden() = _ActivitiesListResultForbidden;

  /// Failed result (network, timeout, server error).
  const factory ActivitiesListResult.failed(AppException error) =
      _ActivitiesListResultFailed;

  _ActivitiesListResultSuccess._();
  _ActivitiesListResultUnauthenticated._();
  _ActivitiesListResultForbidden._();
  _ActivitiesListResultFailed._();
}

/// Result of a [ActivitiesRepository.detailActivity] call.
sealed class ActivitiesDetailResult {
  const ActivitiesDetailResult._();

  /// Successful result with an activity detail DTO.
  const factory ActivitiesDetailResult.success(ActivityDto activity) =
      _ActivitiesDetailResultSuccess;

  /// Unauthenticated result (session expired / no token).
  const factory ActivitiesDetailResult.unauthenticated() =
      _ActivitiesDetailResultUnauthenticated;

  /// Not found result (activity does not exist).
  const factory ActivitiesDetailResult.notFound() =
      _ActivitiesDetailResultNotFound;

  /// Forbidden result (403 - member lacks permission).
  const factory ActivitiesDetailResult.forbidden() = _ActivitiesDetailResultForbidden;

  /// Failed result (network, timeout, server error).
  const factory ActivitiesDetailResult.failed(AppException error) =
      _ActivitiesDetailResultFailed;

  _ActivitiesDetailResultSuccess._();
  _ActivitiesDetailResultUnauthenticated._();
  _ActivitiesDetailResultNotFound._();
  _ActivitiesDetailResultForbidden._();
  _ActivitiesDetailResultFailed._();
}