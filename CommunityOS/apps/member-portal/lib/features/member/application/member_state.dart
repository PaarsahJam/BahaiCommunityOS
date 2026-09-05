import 'package:freezed_annotation/freezed_annotation.dart';

import '../../../core/error/app_exception.dart';
import '../data/member_dtos.dart';
import '../domain/member_models.dart';

part 'member_state.freezed.dart';

@freezed
sealed class MemberState with _$MemberState {
  const MemberState._();

  const factory MemberState.initial() = MemberInitial;

  const factory MemberState.loading() = MemberLoading;

  const factory MemberState.loaded(MemberHomeData data) = MemberLoaded;

  const factory MemberState.unlinked() = MemberUnlinked;

  const factory MemberState.sessionExpired() = MemberSessionExpired;

  const factory MemberState.forbidden(AppException error) = MemberForbidden;

  const factory MemberState.failed(AppException error) = MemberFailed;

  const factory MemberState.profileLoading() = MemberProfileLoading;

  const factory MemberState.profileLoaded(PersonDetailDto detail) =
      MemberProfileLoaded;

  const factory MemberState.profileFailed(AppException error) =
      MemberProfileFailed;
}
