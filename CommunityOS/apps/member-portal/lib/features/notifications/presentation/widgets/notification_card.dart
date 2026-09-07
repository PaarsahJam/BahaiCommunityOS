import 'package:flutter/material.dart';
import 'package:intl/intl.dart';
import 'package:member_portal/l10n/generated/app_localizations.dart';

import '../../application/notification_state.dart';
import '../../data/notifications_dtos.dart';
import 'notification_type_labels.dart';

/// One notification card in the Notification Center.
///
/// Renders the backend's authoritative [MemberNotificationSummaryDto]
/// verbatim: read state comes from `isRead`/`readAt` (never inferred), Title and
/// Body are the server's content (never replaced with the TypeCode or a
/// synthesized message), and an empty Body stays empty.
///
/// Unread/read is not conveyed by style alone — an explicit localized label is
/// shown — and the explicit "Mark as read" action drives the only read-state
/// transition, which marks nothing merely on view.
class NotificationCard extends StatelessWidget {
  const NotificationCard({
    super.key,
    required this.notification,
    required this.status,
    required this.onMarkRead,
  });

  final MemberNotificationSummaryDto notification;
  final MarkReadStatus status;
  final VoidCallback onMarkRead;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final l10n = AppLocalizations.of(context)!;
    final unread = !notification.isRead;
    final submitting = status is MarkReadSubmitting;
    final failed = status is MarkReadFailed;
    final dateFormat = DateFormat.yMMMd(l10n.localeName);

    final unreadLabel =
        unread ? l10n.notificationsUnread : l10n.notificationsRead;

    return Card(
      margin: const EdgeInsets.only(bottom: 8),
      child: Semantics(
        container: true,
        label: '${notificationTypeLabel(context, notification.typeCode)}, '
            '${notification.title}, '
            '${notification.body}, '
            '$unreadLabel',
        child: Padding(
          padding: const EdgeInsets.all(16),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Row(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Expanded(
                    child: Wrap(
                      spacing: 8,
                      runSpacing: 8,
                      children: [
                        Chip(
                          avatar: Icon(
                            Icons.label_outline,
                            size: 18,
                            color: theme.colorScheme.outline,
                          ),
                          label: Text(
                            notificationTypeLabel(
                                context, notification.typeCode),
                          ),
                        ),
                        if (unread)
                          Chip(
                            avatar: Icon(
                              Icons.circle,
                              size: 12,
                              color: theme.colorScheme.primary,
                            ),
                            label: Text(
                              l10n.notificationsUnread,
                              style: theme.textTheme.labelMedium,
                            ),
                          )
                        else
                          Chip(
                            avatar: Icon(
                              Icons.check_circle_outline,
                              size: 18,
                              color: theme.colorScheme.outline,
                            ),
                            label: Text(
                              l10n.notificationsRead,
                              style: theme.textTheme.labelMedium,
                            ),
                          ),
                      ],
                    ),
                  ),
                ],
              ),
              const SizedBox(height: 8),
              if (notification.title.trim().isNotEmpty)
                Text(
                  notification.title,
                  style: theme.textTheme.titleMedium?.copyWith(
                    fontWeight: unread ? FontWeight.w700 : FontWeight.w500,
                  ),
                ),
              if (notification.title.trim().isNotEmpty &&
                  notification.body.trim().isNotEmpty)
                const SizedBox(height: 4),
              // Body renders fully (never truncated in a way that hides text).
              if (notification.body.trim().isNotEmpty)
                Text(notification.body, style: theme.textTheme.bodyMedium),
              const SizedBox(height: 8),
              Text(
                '${l10n.notificationsReceived}: '
                '${dateFormat.format(notification.createdOn)}',
                style: theme.textTheme.bodySmall,
              ),
              const SizedBox(height: 8),
              if (unread && !submitting)
                OutlinedButton.icon(
                  key: const Key('mark-read'),
                  onPressed: onMarkRead,
                  icon: const Icon(Icons.done_all),
                  label: Text(l10n.notificationsMarkAsRead),
                ),
              if (unread && submitting)
                OutlinedButton.icon(
                  onPressed: null,
                  icon: const SizedBox(
                    width: 18,
                    height: 18,
                    child: CircularProgressIndicator(strokeWidth: 2),
                  ),
                  label: Text(l10n.notificationsMarkingAsRead),
                ),
              if (failed)
                Padding(
                  padding: const EdgeInsets.only(top: 8),
                  child: _MarkReadError(
                    message: l10n.notificationsMarkReadFailed,
                  ),
                ),
            ],
          ),
        ),
      ),
    );
  }
}

/// Localized mark-read failure, announced as a live region. The server's
/// problem-detail is deliberately not surfaced here; a fixed localized message
/// is shown and the mark-as-read control stays available to retry.
class _MarkReadError extends StatelessWidget {
  const _MarkReadError({required this.message});

  final String message;

  @override
  Widget build(BuildContext context) {
    return Semantics(
      container: true,
      liveRegion: true,
      child: Text(
        message,
        style: TextStyle(color: Theme.of(context).colorScheme.error),
      ),
    );
  }
}
