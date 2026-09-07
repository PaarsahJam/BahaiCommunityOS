import 'package:freezed_annotation/freezed_annotation.dart';

part 'notification_event.freezed.dart';

/// Events for one page-scoped [NotificationBloc] instance.
///
/// Ownership follows the frozen architecture: the bloc only loads the
/// authenticated member's notifications, drives bounded pagination and applies
/// the explicit mark-as-read operation. The backend is the single authority on
/// scope, read state and the unread count; authentication transitions remain
/// exclusively the responsibility of `AuthBloc`.
@freezed
sealed class NotificationEvent with _$NotificationEvent {
  /// Loads the first page of the authenticated member's notifications and the
  /// authoritative unread count.
  const factory NotificationEvent.requested() = NotificationRequested;

  /// Loads the next page when more results may exist.
  const factory NotificationEvent.loadMoreRequested() =
      NotificationLoadMoreRequested;

  /// Marks a single notification read with `POST /my-notifications/{id}/read`.
  /// The authenticated actor is the recipient; no member identifier is ever
  /// supplied.
  const factory NotificationEvent.markReadRequested({required String id}) =
      NotificationMarkReadRequested;

  /// Refreshes the authoritative unread count from the dedicated endpoint.
  const factory NotificationEvent.unreadRefreshRequested() =
      NotificationUnreadRefreshRequested;
}
