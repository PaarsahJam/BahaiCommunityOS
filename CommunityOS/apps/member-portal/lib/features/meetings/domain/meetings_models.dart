import '../../../core/error/app_exception.dart';
import '../data/meetings_dtos.dart';

/// Outcome of a [MeetingsRepository.listMeetings] call.
///
/// A successful result (which may carry an empty list) is represented by
/// [MeetingsListResult.success]; authorization, network and server failures are
/// represented by their dedicated variants and are never conflated with an
/// empty list.
sealed class MeetingsListResult {
  const MeetingsListResult._();

  /// Successful result with the list of available meetings.
  const factory MeetingsListResult.success(List<MeetingDto> meetings) =
      _MeetingsListResultSuccess;

  /// Unauthenticated result (session expired / no token).
  const factory MeetingsListResult.unauthenticated() =
      _MeetingsListResultUnauthenticated;

  /// Forbidden result (403 - member lacks permission).
  const factory MeetingsListResult.forbidden() = _MeetingsListResultForbidden;

  /// Network, timeout or server failure.
  const factory MeetingsListResult.failed(AppException error) =
      _MeetingsListResultFailed;

  /// Collapses the outcome into one value for two-branch handling.
  R fold<R>(
    R Function(AppException failure) onFailure,
    R Function(List<MeetingDto> meetings) onSuccess,
  ) =>
      switch (this) {
        _MeetingsListResultSuccess(:final meetings) => onSuccess(meetings),
        _MeetingsListResultUnauthenticated() =>
          onFailure(const UnauthorizedException('Unauthenticated')),
        _MeetingsListResultForbidden() =>
          onFailure(const ForbiddenException('Forbidden')),
        _MeetingsListResultFailed(:final error) => onFailure(error),
      };
}

final class _MeetingsListResultSuccess extends MeetingsListResult {
  const _MeetingsListResultSuccess(this.meetings) : super._();

  final List<MeetingDto> meetings;
}

final class _MeetingsListResultUnauthenticated extends MeetingsListResult {
  const _MeetingsListResultUnauthenticated() : super._();
}

final class _MeetingsListResultForbidden extends MeetingsListResult {
  const _MeetingsListResultForbidden() : super._();
}

final class _MeetingsListResultFailed extends MeetingsListResult {
  const _MeetingsListResultFailed(this.error) : super._();

  final AppException error;
}

/// Outcome of a [MeetingsRepository.getMeetingById] call.
sealed class MeetingsDetailResult {
  const MeetingsDetailResult._();

  /// Successful result with a meeting detail DTO.
  const factory MeetingsDetailResult.success(MeetingDto meeting) =
      _MeetingsDetailResultSuccess;

  /// Unauthenticated result (session expired / no token).
  const factory MeetingsDetailResult.unauthenticated() =
      _MeetingsDetailResultUnauthenticated;

  /// Not found result (the meeting does not exist).
  const factory MeetingsDetailResult.notFound() = _MeetingsDetailResultNotFound;

  /// Forbidden result (403 - member lacks permission).
  const factory MeetingsDetailResult.forbidden() = _MeetingsDetailResultForbidden;

  /// Network, timeout or server failure.
  const factory MeetingsDetailResult.failed(AppException error) =
      _MeetingsDetailResultFailed;

  /// Collapses the outcome into one value for two-branch handling.
  R fold<R>(
    R Function(AppException failure) onFailure,
    R Function(MeetingDto meeting) onSuccess,
  ) =>
      switch (this) {
        _MeetingsDetailResultSuccess(:final meeting) => onSuccess(meeting),
        _MeetingsDetailResultUnauthenticated() =>
          onFailure(const UnauthorizedException('Unauthenticated')),
        _MeetingsDetailResultForbidden() =>
          onFailure(const ForbiddenException('Forbidden')),
        _MeetingsDetailResultNotFound() =>
          onFailure(const NotFoundException('Meeting not found')),
        _MeetingsDetailResultFailed(:final error) => onFailure(error),
      };
}

final class _MeetingsDetailResultSuccess extends MeetingsDetailResult {
  const _MeetingsDetailResultSuccess(this.meeting) : super._();

  final MeetingDto meeting;
}

final class _MeetingsDetailResultUnauthenticated extends MeetingsDetailResult {
  const _MeetingsDetailResultUnauthenticated() : super._();
}

final class _MeetingsDetailResultNotFound extends MeetingsDetailResult {
  const _MeetingsDetailResultNotFound() : super._();
}

final class _MeetingsDetailResultForbidden extends MeetingsDetailResult {
  const _MeetingsDetailResultForbidden() : super._();
}

final class _MeetingsDetailResultFailed extends MeetingsDetailResult {
  const _MeetingsDetailResultFailed(this.error) : super._();

  final AppException error;
}