import 'package:freezed_annotation/freezed_annotation.dart';

part 'profile_event.freezed.dart';

@freezed
sealed class ProfileEvent with _$ProfileEvent {
  /// Requests a person's profile detail for [personId]. The identifier is the
  /// Community PersonId obtained from `/my-person` for the member's own
  /// profile; it is never derived from the JWT or assumed equal to the
  /// UserAccountId. The backend authorizes the access.
  const factory ProfileEvent.requested({required String personId}) =
      ProfileRequested;
}
