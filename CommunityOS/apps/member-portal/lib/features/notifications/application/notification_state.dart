import 'package:freezed_annotation/freezed_annotation.dart';

import '../../../core/error/app_exception.dart';
import '../data/notifications_dtos.dart';

part 'notification_state.freezed.dart';

/// Per-notification mark-as-read lifecycle rendered by the notification list.
///
/// The backend's returned DTO is the single authority for read state; a failure
/// (distinguished here) leaves the original unread state untouched.
@freezed
sealed class MarkReadStatus with _$MarkReadStatus {
  const factory MarkReadStatus.idle() = MarkReadIdle;
  const factory MarkReadStatus.submitting() = MarkReadSubmitting;
  const factory MarkReadStatus.succeeded() = MarkReadSucceeded;
  const factory MarkReadStatus.failed({AppException? error}) = MarkReadFailed;
}

/// Page-scoped state for the Notification Center.
///
/// [items] are backend responses verbatim. [unreadCount] is always the
/// dedicated endpoint's value; it is never derived from the loaded page, and a
/// failed count or a failed list request is never rendered as zero/empty.
@freezed
sealed class NotificationState with _$NotificationState {
  const factory NotificationState.initial() = NotificationInitial;

  const factory NotificationState.loading() = NotificationLoading;

  const factory NotificationState.loaded({
    required List<MemberNotificationSummaryDto> items,

    /// Whether more results may be loaded (the last page returned a full page).
    /// The contract exposes no total/hasMore field, so a page shorter than the
    /// requested limit is the conservative terminal-page signal.
    @Default(false) bool hasMore,
    @Default(false) bool isLoadingMore,
    AppException? loadMoreError,
    int? unreadCount,
    @Default(false) bool isUnreadCountLoading,
    AppException? unreadCountError,
    @Default(<String, MarkReadStatus>{})
    Map<String, MarkReadStatus> markReadStatuses,
  }) = NotificationLoaded;

  const factory NotificationState.failed(AppException error) =
      NotificationFailed;
}
