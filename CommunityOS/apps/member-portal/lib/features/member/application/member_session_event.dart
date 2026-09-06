import 'package:freezed_annotation/freezed_annotation.dart';

part 'member_session_event.freezed.dart';

@freezed
sealed class MemberSessionEvent with _$MemberSessionEvent {
  /// Requests the authenticated member-context bootstrap (`/me`, `/my-person`,
  /// `/memberships/by-person/{personId}`). Fired when the member shell mounts.
  const factory MemberSessionEvent.bootstrapRequested() =
      MemberSessionBootstrapRequested;
}
