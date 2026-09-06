import 'package:freezed_annotation/freezed_annotation.dart';

part 'membership_event.freezed.dart';

@freezed
sealed class MembershipEvent with _$MembershipEvent {
  /// Requests the authoritative membership record for [personId] — the
  /// Community PersonId obtained from the authenticated `MemberContext`; it is
  /// never derived from the JWT or supplied by a route parameter or user input.
  /// The backend authorizes the access.
  const factory MembershipEvent.requested({required String personId}) =
      MembershipRequested;
}
