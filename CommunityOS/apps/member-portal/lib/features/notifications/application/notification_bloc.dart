import 'package:bloc/bloc.dart';

import '../../../core/error/app_exception.dart';
import '../data/notifications_dtos.dart';
import '../domain/notification_repository.dart';
import 'notification_event.dart';
import 'notification_state.dart';

/// Loads the authenticated member's notifications, drives bounded pagination,
/// and applies the explicit mark-as-read operation for one Notification Center
/// page instance.
///
/// Ownership follows the frozen architecture: this bloc is page-scoped
/// (created and closed by the page), never global, and never emits
/// authentication transitions — `AuthBloc` and the centralized session-expiry
/// machinery exclusively own those. The backend is the single authority for
/// scope, content, read state and the unread count; a page shorter than the
/// requested limit is the conservative terminal-page signal because the
/// contract exposes no total/hasMore field.
class NotificationBloc extends Bloc<NotificationEvent, NotificationState> {
  NotificationBloc(this._repository)
      : super(const NotificationState.initial()) {
    on<NotificationRequested>(_onRequested);
    on<NotificationLoadMoreRequested>(_onLoadMore);
    on<NotificationMarkReadRequested>(_onMarkRead);
    on<NotificationUnreadRefreshRequested>(_onUnreadRefresh);
  }

  final NotificationRepository _repository;

  /// Bounded page size matching the backend's default limit for the member
  /// surface. The list is never requested unbounded.
  static const int pageSize = 50;

  /// Notifications with a mark-read request currently in flight, guarding
  /// against duplicate submissions for the same notification.
  final Set<String> _marking = {};

  Future<void> _onRequested(
    NotificationRequested event,
    Emitter<NotificationState> emit,
  ) async {
    emit(const NotificationState.loading());
    try {
      final items =
          await _repository.myNotifications(limit: pageSize, offset: 0);
      // The page may have been popped (disposed) while loading; a closed bloc
      // must never emit.
      if (isClosed) return;
      emit(NotificationState.loaded(
        items: items,
        hasMore: items.length == pageSize,
      ));
      await _loadUnread(emit);
    } on AppException catch (error) {
      if (isClosed) return;
      // A failed list request is never rendered as an empty list.
      emit(NotificationState.failed(error));
    }
  }

  Future<void> _onLoadMore(
    NotificationLoadMoreRequested event,
    Emitter<NotificationState> emit,
  ) async {
    final current = state;
    if (current is! NotificationLoaded) return;
    // Never page past the terminal page and never run duplicate pagination
    // requests concurrently.
    if (!current.hasMore || current.isLoadingMore) return;

    emit(current.copyWith(isLoadingMore: true, loadMoreError: null));
    try {
      final batch = await _repository.myNotifications(
        limit: pageSize,
        offset: current.items.length,
      );
      if (isClosed) return;
      final loaded = state as NotificationLoaded;
      emit(loaded.copyWith(
        items: [...loaded.items, ...batch],
        isLoadingMore: false,
        hasMore: batch.length == pageSize,
      ));
    } on AppException catch (error) {
      if (isClosed) return;
      // Keep the loaded list on screen and surface a retryable load-more error.
      final loaded = state as NotificationLoaded;
      emit(loaded.copyWith(isLoadingMore: false, loadMoreError: error));
    }
  }

  Future<void> _onMarkRead(
    NotificationMarkReadRequested event,
    Emitter<NotificationState> emit,
  ) async {
    final current = state;
    if (current is! NotificationLoaded) return;

    final notification = _find(current.items, event.id);
    if (notification == null) return;
    // Avoid the unnecessary mutation: the backend rejects an already-read
    // notification with a conflict.
    if (notification.isRead) return;
    if (_marking.contains(event.id)) return;
    if (current.markReadStatuses[event.id] is MarkReadSubmitting) return;

    _marking.add(event.id);
    emit(current.copyWith(
      markReadStatuses: {
        ...current.markReadStatuses,
        event.id: const MarkReadStatus.submitting(),
      },
    ));

    try {
      final updated = await _repository.markRead(event.id);
      if (isClosed) return;
      _marking.remove(event.id);
      final loaded = state as NotificationLoaded;
      // Replace the item with the backend's authoritative DTO (isRead/readAt).
      emit(loaded.copyWith(
        items: [
          for (final item in loaded.items)
            if (item.id == event.id) updated else item,
        ],
        markReadStatuses: {
          ...loaded.markReadStatuses,
          event.id: const MarkReadStatus.succeeded(),
        },
      ));
      await _loadUnread(emit);
    } on AppException catch (error) {
      if (isClosed) return;
      _marking.remove(event.id);
      final loaded = state as NotificationLoaded;
      // A failed mark-read preserves the original unread state.
      emit(loaded.copyWith(
        markReadStatuses: {
          ...loaded.markReadStatuses,
          event.id: MarkReadStatus.failed(error: error),
        },
      ));
    }
  }

  Future<void> _onUnreadRefresh(
    NotificationUnreadRefreshRequested event,
    Emitter<NotificationState> emit,
  ) =>
      _loadUnread(emit);

  Future<void> _loadUnread(Emitter<NotificationState> emit) async {
    final current = state;
    if (current is! NotificationLoaded) return;

    emit(current.copyWith(isUnreadCountLoading: true, unreadCountError: null));
    try {
      final count = await _repository.unreadCount();
      if (isClosed) return;
      final loaded = state as NotificationLoaded;
      emit(loaded.copyWith(
        unreadCount: count,
        isUnreadCountLoading: false,
      ));
    } on AppException catch (error) {
      if (isClosed) return;
      // Never turn a failed count into zero.
      final loaded = state as NotificationLoaded;
      emit(loaded.copyWith(
        isUnreadCountLoading: false,
        unreadCountError: error,
      ));
    }
  }

  MemberNotificationSummaryDto? _find(
    List<MemberNotificationSummaryDto> items,
    String id,
  ) {
    for (final item in items) {
      if (item.id == id) return item;
    }
    return null;
  }
}
