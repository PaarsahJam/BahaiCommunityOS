import 'package:freezed_annotation/freezed_annotation.dart';

part 'activities_event.freezed.dart';

/// Events for one page-scoped activities bloc instance.
///
/// Ownership follows the frozen architecture: the bloc only loads the
/// authenticated member's activities and drives the read-only list filters;
/// authentication transitions remain exclusively the responsibility of the
/// auth bloc and the centralized session-expiry machinery.
@freezed
sealed class ActivitiesEvent with _$ActivitiesEvent {
  /// Loads the activity list, optionally narrowed by the verified backend
  /// filters. All parameters default to unset, so the read-only slice issues
  /// the unfiltered request.
  const factory ActivitiesEvent.requested({
    DateTime? from,
    DateTime? to,
    String? organizationUnitId,
  }) = ActivitiesRequested;

  /// Loads a single activity's details by [id].
  const factory ActivitiesEvent.detailRequested({required String id}) =
      ActivitiesDetailRequested;
}
