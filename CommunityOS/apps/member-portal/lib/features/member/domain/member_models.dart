import 'package:freezed_annotation/freezed_annotation.dart';

import '../../auth/domain/auth_models.dart';
import '../data/member_dtos.dart';

part 'member_models.freezed.dart';

/// Authenticated member context resolved during the shell bootstrap: the
/// Identity account (from `/me`), the linked Community person (from
/// `/my-person`), and the optional membership record (from
/// `/memberships/by-person/{personId}`).
///
/// A null [membership] is the legitimate "no membership record" outcome and is
/// never synthesized from a failure; failures are represented by the
/// [MemberSessionResult] variants instead.
class MemberContext {
  const MemberContext({
    required this.account,
    required this.person,
    this.membership,
  });

  final AuthUser account;
  final PersonDto person;
  final MembershipDto? membership;

  bool get hasMembership => membership != null;
}

/// Typed outcome of the member-session bootstrap.
///
/// * [MemberSessionResolved] — account + person + (membership | none).
/// * [MemberSessionUnlinked] — authenticated account with no linked Community
///   person (`/my-person` returned 404). This is NOT a sign-out condition.
/// * [MemberSessionUnauthenticated] — the session is invalid/expired (401).
/// * [MemberSessionForbidden] — authenticated but not permitted (403).
/// * [MemberSessionFailed] — network/timeout/server failure (never conflated
///   with "no membership").
@freezed
sealed class MemberSessionResult with _$MemberSessionResult {
  const factory MemberSessionResult.resolved({
    required AuthUser account,
    required PersonDto person,
    MembershipDto? membership,
  }) = MemberSessionResolved;

  const factory MemberSessionResult.unlinked() = MemberSessionUnlinked;

  const factory MemberSessionResult.unauthenticated() =
      MemberSessionUnauthenticated;

  const factory MemberSessionResult.forbidden(AppException error) =
      MemberSessionForbidden;

  const factory MemberSessionResult.failed(AppException error) =
      MemberSessionFailed;
}
