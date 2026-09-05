import 'package:freezed_annotation/freezed_annotation.dart';

import '../../../core/error/app_exception.dart';
import '../data/member_dtos.dart';

part 'member_models.freezed.dart';

/// Aggregated data displayed on the member home screen.
class MemberHomeData {
  const MemberHomeData({required this.person, this.membership});

  final PersonDto person;
  final MembershipDto? membership;

  bool get hasMembership => membership != null;
}

/// Typed outcome of loading the member home slice (identity → person →
/// membership). The 404 "profile not linked" case is a first-class result
/// rather than an error so the UI can present a dedicated state.
@freezed
sealed class MemberHomeResult with _$MemberHomeResult {
  const factory MemberHomeResult.resolved({
    required PersonDto person,
    MembershipDto? membership,
  }) = MemberHomeResolved;

  const factory MemberHomeResult.unlinkedAccount() = MemberHomeUnlinked;

  const factory MemberHomeResult.unauthenticated() = MemberHomeUnauthenticated;

  const factory MemberHomeResult.forbidden(AppException error) =
      MemberHomeForbidden;

  const factory MemberHomeResult.failed(AppException error) = MemberHomeFailed;
}
