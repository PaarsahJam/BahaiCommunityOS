import 'package:flutter/material.dart';
import 'package:flutter_bloc/flutter_bloc.dart';
import 'package:member_portal/l10n/generated/app_localizations.dart';

import '../../../core/error/app_exception.dart';
import '../../../core/ui/exception_message.dart';
import '../../../di/injection.dart';
import '../application/notification_bloc.dart';
import '../application/notification_event.dart';
import '../application/notification_state.dart';
import '../domain/notification_repository.dart';
import 'widgets/notification_card.dart';

/// The authenticated member's Notification Center.
///
/// Renders `GET /my-notifications` pages, the authoritative unread count, and
/// drives the explicit mark-as-read operation. This page is scoped to a
/// [NotificationBloc] instance it creates and closes itself (via the injected
/// default factory) — never a global bloc — and the backend remains the single
/// authority for scope, content, read state and the unread count. The page
/// participates in the existing deep-link flow through `/notifications`.
class NotificationCenterPage extends StatefulWidget {
  const NotificationCenterPage({super.key, this.createBloc});

  /// Injected bloc factory (defaults to get_it) so tests can substitute a
  /// scripted bloc without touching global state.
  final NotificationBloc Function()? createBloc;

  @override
  State<NotificationCenterPage> createState() => _NotificationCenterPageState();
}

class _NotificationCenterPageState extends State<NotificationCenterPage> {
  late final NotificationBloc _bloc = (widget.createBloc ?? _defaultBloc)();

  static NotificationBloc _defaultBloc() {
    return NotificationBloc(getIt<NotificationRepository>());
  }

  @override
  void initState() {
    super.initState();
    _bloc.add(const NotificationEvent.requested());
  }

  @override
  void dispose() {
    _bloc.close();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    return Scaffold(
      appBar: AppBar(
        title: Text(AppLocalizations.of(context)!.notificationsTitle),
      ),
      body: BlocProvider<NotificationBloc>.value(
        value: _bloc,
        child: BlocBuilder<NotificationBloc, NotificationState>(
          builder: (context, state) {
            return switch (state) {
              NotificationInitial() => const SizedBox.shrink(),
              NotificationLoading() =>
                const Center(child: CircularProgressIndicator()),
              NotificationFailed(:final error) => _NotificationErrorView(
                  error: error,
                  onRetry: () => context
                      .read<NotificationBloc>()
                      .add(const NotificationEvent.requested()),
                ),
              NotificationLoaded() => _NotificationList(state: state),
            };
          },
        ),
      ),
    );
  }
}

/// Rendered in place of a list that could not be loaded. Never presents a
/// failed list request as an empty one.
class _NotificationErrorView extends StatelessWidget {
  const _NotificationErrorView({required this.error, this.onRetry});

  final AppException error;
  final VoidCallback? onRetry;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final l10n = AppLocalizations.of(context)!;
    return Center(
      child: Padding(
        padding: const EdgeInsets.all(24),
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: [
            Icon(Icons.error_outline, size: 48, color: theme.colorScheme.error),
            const SizedBox(height: 12),
            Text(exceptionMessage(context, error), textAlign: TextAlign.center),
            if (onRetry != null) ...[
              const SizedBox(height: 16),
              OutlinedButton.icon(
                onPressed: onRetry,
                icon: const Icon(Icons.refresh),
                label: Text(l10n.notificationsRefresh),
              ),
            ],
          ],
        ),
      ),
    );
  }
}

class _NotificationList extends StatelessWidget {
  const _NotificationList({required this.state});

  final NotificationLoaded state;

  @override
  Widget build(BuildContext context) {
    final bloc = context.read<NotificationBloc>();

    return RefreshIndicator(
      onRefresh: () async {
        bloc.add(const NotificationEvent.requested());
        await bloc.stream.firstWhere((s) => s is! NotificationLoading);
      },
      child: state.items.isEmpty
          ? _EmptyNotifications(
              onRetry: () => bloc.add(const NotificationEvent.requested()),
            )
          : ListView.builder(
              key: const Key('notification-list'),
              padding: const EdgeInsets.all(16),
              itemCount: state.items.length + 1,
              itemBuilder: (context, index) {
                if (index == state.items.length) {
                  return Column(
                    crossAxisAlignment: CrossAxisAlignment.stretch,
                    children: [
                      _LoadMoreControl(
                        state: state,
                        onLoadMore: () => bloc
                            .add(const NotificationEvent.loadMoreRequested()),
                      ),
                      _UnreadSummary(
                        count: state.unreadCount,
                        isLoading: state.isUnreadCountLoading,
                        hasError: state.unreadCountError != null,
                        onRefresh: () => bloc.add(
                            const NotificationEvent.unreadRefreshRequested()),
                      ),
                    ],
                  );
                }
                final notification = state.items[index];
                final status = state.markReadStatuses[notification.id] ??
                    const MarkReadStatus.idle();
                return NotificationCard(
                  notification: notification,
                  status: status,
                  onMarkRead: () => bloc.add(
                    NotificationEvent.markReadRequested(id: notification.id),
                  ),
                );
              },
            ),
    );
  }
}

/// Explicit bounded pagination control. Requests stop permanently at the
/// terminal page; a failed additional page keeps the loaded list visible and
/// offers retry rather than replacing anything.
class _LoadMoreControl extends StatelessWidget {
  const _LoadMoreControl({required this.state, required this.onLoadMore});

  final NotificationLoaded state;
  final VoidCallback onLoadMore;

  @override
  Widget build(BuildContext context) {
    final l10n = AppLocalizations.of(context)!;
    final error = state.loadMoreError;

    if (error != null) {
      return Padding(
        padding: const EdgeInsets.symmetric(vertical: 8),
        child: Column(
          children: [
            Text(
              l10n.notificationsLoadMoreFailed,
              style: TextStyle(color: Theme.of(context).colorScheme.error),
            ),
            TextButton.icon(
              key: const Key('retry-load-more'),
              onPressed: onLoadMore,
              icon: const Icon(Icons.refresh),
              label: Text(l10n.notificationsLoadMore),
            ),
          ],
        ),
      );
    }

    if (state.isLoadingMore) {
      return Padding(
        padding: const EdgeInsets.symmetric(vertical: 24),
        child: Center(
          child: Semantics(
            label: '',
            child: const SizedBox(
              width: 20,
              height: 20,
              child: CircularProgressIndicator(strokeWidth: 2),
            ),
          ),
        ),
      );
    }

    if (!state.hasMore) {
      // Terminal page reached: request permanently stops here.
      return const SizedBox.shrink();
    }

    return Padding(
      padding: const EdgeInsets.symmetric(vertical: 8),
      child: Center(
        child: OutlinedButton.icon(
          key: const Key('load-more'),
          onPressed: onLoadMore,
          icon: const Icon(Icons.expand_more),
          label: Text(l10n.notificationsLoadMore),
        ),
      ),
    );
  }
}

/// The authoritative unread summary, derived from nothing local. A failed
/// count is surfaced as "unavailable" with a retry — never rendered as zero.
class _UnreadSummary extends StatelessWidget {
  const _UnreadSummary({
    required this.count,
    required this.isLoading,
    required this.hasError,
    required this.onRefresh,
  });

  final int? count;
  final bool isLoading;
  final bool hasError;
  final VoidCallback onRefresh;

  @override
  Widget build(BuildContext context) {
    final l10n = AppLocalizations.of(context)!;
    final theme = Theme.of(context);

    final String message = switch ((count, hasError)) {
      (final int c, false) => l10n.notificationsUnreadHeader(c),
      _ => l10n.notificationsUnreadCountUnavailable,
    };

    return Padding(
      padding: const EdgeInsets.all(16),
      child: Row(
        children: [
          Expanded(
            child: Text(
              message,
              style: theme.textTheme.bodySmall,
            ),
          ),
          if (hasError)
            TextButton(
              key: const Key('refresh-unread-count'),
              onPressed: isLoading ? null : onRefresh,
              child: Text(l10n.notificationsRefresh),
            ),
          if (isLoading)
            Padding(
              padding: const EdgeInsets.symmetric(horizontal: 12),
              child: Semantics(
                label: '',
                child: const SizedBox(
                  width: 16,
                  height: 16,
                  child: CircularProgressIndicator(strokeWidth: 2),
                ),
              ),
            ),
        ],
      ),
    );
  }
}

class _EmptyNotifications extends StatelessWidget {
  const _EmptyNotifications({required this.onRetry});

  final VoidCallback onRetry;

  @override
  Widget build(BuildContext context) {
    final l10n = AppLocalizations.of(context)!;
    return Center(
      child: Column(
        mainAxisAlignment: MainAxisAlignment.center,
        children: [
          const Icon(Icons.notifications_none, size: 48),
          const SizedBox(height: 8),
          Text(l10n.notificationsEmpty),
          const SizedBox(height: 16),
          OutlinedButton(
            onPressed: onRetry,
            child: Text(l10n.notificationsRefresh),
          ),
        ],
      ),
    );
  }
}
