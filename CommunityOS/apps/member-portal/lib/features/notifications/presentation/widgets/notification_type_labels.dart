import 'package:flutter/widgets.dart';
import 'package:member_portal/l10n/generated/app_localizations.dart';

/// Localized label for a notification `TypeCode` returned by the Community
/// service.
///
/// Only codes established by the backend source are mapped. An unknown code is
/// presented with the neutral "Notification" fallback — never fabricated into a
/// semantic label, never treated as executable behavior. Matching is
/// case-insensitive because the service emits kebab-case codes without a casing
/// contract.
String notificationTypeLabel(BuildContext context, String typeCode) {
  final l10n = AppLocalizations.of(context);
  if (l10n == null) return typeCode;
  return switch (typeCode.toLowerCase()) {
    'task-assigned' => l10n.notificationTypeTaskAssigned,
    'task-escalated' => l10n.notificationTypeTaskEscalated,
    'task-completed' => l10n.notificationTypeTaskCompleted,
    'task-cancelled' => l10n.notificationTypeTaskCancelled,
    'record-verified' => l10n.notificationTypeRecordVerified,
    'record-rejected' => l10n.notificationTypeRecordRejected,
    'record-hold' => l10n.notificationTypeRecordHold,
    'question-flagged' => l10n.notificationTypeQuestionFlagged,
    'community-activity' => l10n.notificationTypeCommunityActivity,
    'community-event' => l10n.notificationTypeCommunityEvent,
    'community-meeting' => l10n.notificationTypeCommunityMeeting,
    'general' => l10n.notificationTypeGeneral,
    _ => l10n.notificationTypeFallback,
  };
}
