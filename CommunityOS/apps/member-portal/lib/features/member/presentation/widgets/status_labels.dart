import 'package:flutter/widgets.dart';
import 'package:member_portal/l10n/generated/app_localizations.dart';

/// Localized label for the membership/person status strings returned by the
/// Community service.
String membershipStatusLabel(BuildContext context, String status) {
  final l10n = AppLocalizations.of(context);
  if (l10n == null) return status;
  return switch (status) {
    'active' => l10n.membershipStatusActive,
    'pending' => l10n.membershipStatusPending,
    'suspended' => l10n.membershipStatusSuspended,
    'lapsed' => l10n.membershipStatusLapsed,
    'withdrawn' => l10n.membershipStatusWithdrawn,
    _ => l10n.membershipStatusUnknown,
  };
}
