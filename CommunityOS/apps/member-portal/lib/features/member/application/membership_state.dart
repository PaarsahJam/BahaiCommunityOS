import 'package:freezed_annotation/freezed_annotation.dart';

import '../../../core/error/app_exception.dart';
import '../data/member_dtos.dart';

part 'membership_state.freezed.dart';

/// Feature state for the authenticated member's membership view. Owned per
/// membership page instance, so membership loading never mutates the session
/// context and returning to the shell can never leave stale membership data
/// behind.
@freezed
sealed class MembershipState with _$MembershipState {
  const MembershipState._();

  const factory MembershipState.initial() = MembershipInitial;

  const factory MembershipState.loading() = MembershipLoading;

  /// [membership] is the backend response verbatim: a record, or `null` for
  /// the legitimate "this authenticated person has no membership record" state
  /// (`200 + null`). `null` never indicates a failure; failures are carried by
  /// [MembershipFailed].
  const factory MembershipState.loaded(MembershipDto? membership) =
      MembershipLoaded;

  const factory MembershipState.failed(AppException error) = MembershipFailed;
}
