import 'package:freezed_annotation/freezed_annotation.dart';

import '../../../core/error/app_exception.dart';
import '../domain/member_models.dart';

part 'member_session_state.freezed.dart';

/// Lifecycle of the authenticated member context within the member shell.
///
/// The shell owns this state; it is bootstrapped once per authenticated session
/// and destroyed when the session ends (shell disposal), so a new sign-in never
/// inherits the previous account's data.
@freezed
sealed class MemberSessionState with _$MemberSessionState {
  const MemberSessionState._();

  const factory MemberSessionState.initial() = MemberSessionInitial;

  const factory MemberSessionState.loading() = MemberSessionLoading;

  /// Account + person + (membership | none) resolved successfully. The
  /// membership lifecycle (pending/active/suspended/lapsed/withdrawn) is
  /// carried explicitly on [MemberContext.membership].
  const factory MemberSessionState.ready(MemberContext context) =
      MemberSessionReady;

  /// Authenticated account with no linked Community person (`/my-person` 404).
  const factory MemberSessionState.unlinked() = MemberSessionUnlinked;

  /// The session is invalid/expired (401). Self-heals through the AuthBloc
  /// session-expiry handling; the shell renders a brief loading state.
  const factory MemberSessionState.sessionExpired() = MemberSessionExpired;

  /// Authenticated but not permitted (403).
  const factory MemberSessionState.forbidden(AppException error) =
      MemberSessionForbidden;

  /// Network, timeout or server failure (never conflated with "no membership").
  const factory MemberSessionState.failed(AppException error) =
      MemberSessionFailed;
}
