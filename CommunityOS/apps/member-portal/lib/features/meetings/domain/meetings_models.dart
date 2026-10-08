/// Result of a [MeetingsRepository.listMeetings] call.
sealed class MeetingsListResult {
  const MeetingsListResult._();

  /// Successful result with a list of meetings.
  const factory MeetingsListResult.success(
    List<MeetingDto> meetings,
  ) = _MeetingsListResultSuccess;

  /// Unauthenticated result (session expired / no token).
  const factory MeetingsListResult.unauthenticated() =
      _MeetingsListResultUnauthenticated;

  /// Forbidden result (403 - member lacks permission).
  const factory MeetingsListResult.forbidden() = _MeetingsListResultForbidden;

  /// Failed result (network, timeout, server error).
  const factory MeetingsListResult.failed(AppException error) =
      _MeetingsListResultFailed;

  _MeetingsListResultSuccess._();
  _MeetingsListResultUnauthenticated._();
  _MeetingsListResultForbidden._();
  _MeetingsListResultFailed._();
}

/// Result of a [MeetingsRepository.getMeetingById] call.
sealed class MeetingsDetailResult {
  const MeetingsDetailResult._();

  /// Successful result with a meeting detail DTO.
  const factory MeetingsDetailResult.success(MeetingDto meeting) =
      _MeetingsDetailResultSuccess;

  /// Unauthenticated result (session expired / no token).
  const factory MeetingsDetailResult.unauthenticated() =
      _MeetingsDetailResultUnauthenticated;

  /// Not found result (meeting does not exist).
  const factory MeetingsDetailResult.notFound() = _MeetingsDetailResultNotFound;

  /// Forbidden result (403 - member lacks permission).
  const factory MeetingsDetailResult.forbidden() = _MeetingsDetailResultForbidden;

  /// Failed result (network, timeout, server error).
  const factory MeetingsDetailResult.failed(AppException error) =
      _MeetingsDetailResultFailed;

  _MeetingsDetailResultSuccess._();
  _MeetingsDetailResultUnauthenticated._();
  _MeetingsDetailResultNotFound._();
  _MeetingsDetailResultForbidden._();
  _MeetingsDetailResultFailed._();
}