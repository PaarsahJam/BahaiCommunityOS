import 'package:freezed_annotation/freezed_annotation.dart';

part 'home_unread_event.freezed.dart';

/// Events for the Home-scoped unread-count [HomeUnreadBloc].
///
/// The count is always fetched from the authoritative endpoint and is scoped to
/// one Home badge instance (page-scoped, never global).
@freezed
sealed class HomeUnreadEvent with _$HomeUnreadEvent {
  /// Refetches the authoritative unread count.
  const factory HomeUnreadEvent.refresh() = HomeUnreadRefresh;
}
