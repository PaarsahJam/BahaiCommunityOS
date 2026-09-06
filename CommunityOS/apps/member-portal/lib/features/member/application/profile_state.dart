import 'package:freezed_annotation/freezed_annotation.dart';

import '../../../core/error/app_exception.dart';
import '../data/member_dtos.dart';

part 'profile_state.freezed.dart';

/// Feature state for a single person profile view. Owned per profile page
/// instance, so profile loading never mutates the member context and returning
/// to the shell can never leave it in a stale profile state.
@freezed
sealed class ProfileState with _$ProfileState {
  const ProfileState._();

  const factory ProfileState.initial() = ProfileInitial;

  const factory ProfileState.loading() = ProfileLoading;

  /// [detail] is the backend response verbatim; nullable/visibility-masked
  /// fields are preserved as absences and never manufactured on the client.
  const factory ProfileState.loaded(PersonDetailDto detail) = ProfileLoaded;

  const factory ProfileState.failed(AppException error) = ProfileFailed;
}
