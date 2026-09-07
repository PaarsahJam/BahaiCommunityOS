import 'package:freezed_annotation/freezed_annotation.dart';

import '../../../core/error/app_exception.dart';

part 'home_unread_state.freezed.dart';

/// State for the Home unread-count badge, scoped to one Home badge instance.
///
/// [HomeUnreadLoaded.count] is always the dedicated endpoint's value. A failure
/// is [HomeUnreadFailed] — it is never rendered as a zero count.
@freezed
sealed class HomeUnreadState with _$HomeUnreadState {
  const factory HomeUnreadState.initial() = HomeUnreadInitial;

  const factory HomeUnreadState.loading() = HomeUnreadLoading;

  const factory HomeUnreadState.loaded(int count) = HomeUnreadLoaded;

  const factory HomeUnreadState.failed(AppException error) = HomeUnreadFailed;
}
