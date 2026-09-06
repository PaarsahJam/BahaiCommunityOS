import 'package:flutter/material.dart';
import 'package:flutter_bloc/flutter_bloc.dart';
import 'package:intl/intl.dart';
import 'package:member_portal/l10n/generated/app_localizations.dart';

import '../../../core/error/app_exception.dart';
import '../../../core/ui/exception_message.dart';
import '../../../di/injection.dart';
import '../application/member_session_bloc.dart';
import '../application/member_session_state.dart';
import '../application/membership_bloc.dart';
import '../application/membership_event.dart';
import '../application/membership_state.dart';
import '../data/member_dtos.dart';
import '../domain/member_repository.dart';
import 'widgets/status_labels.dart';

/// Member membership feature, rendered inside the authenticated shell.
///
/// The authoritative [personId] is obtained from the ready `MemberContext` (the
/// Community PersonId resolved by `/my-person`); it is never derived from the
/// JWT and never accepted from a route parameter or user input. The backend
/// authorizes the access; `200 + null` is the legitimate no-membership state
/// and every failure is rendered as a failure — never as an absent record.
class MembershipPage extends StatefulWidget {
  const MembershipPage({super.key, this.createMembershipBloc});

  /// Injectable factory for tests; defaults to a bloc backed by the DI
  /// [MemberRepository].
  final MembershipBloc Function()? createMembershipBloc;

  @override
  State<MembershipPage> createState() => _MembershipPageState();
}

class _MembershipPageState extends State<MembershipPage> {
  late final MembershipBloc _bloc = (widget.createMembershipBloc ??
      () => MembershipBloc(getIt<MemberRepository>()))();

  String? get _personId {
    final state = context.read<MemberSessionBloc>().state;
    return switch (state) {
      MemberSessionReady(:final context) => context.person.id,
      _ => null,
    };
  }

  @override
  void initState() {
    super.initState();
    WidgetsBinding.instance.addPostFrameCallback((_) {
      if (!mounted) return;
      final personId = _personId;
      if (personId != null) {
        _bloc.add(MembershipEvent.requested(personId: personId));
      }
    });
  }

  @override
  void dispose() {
    _bloc.close();
    super.dispose();
  }

  void _retry() {
    final personId = _personId;
    if (personId != null) {
      _bloc.add(MembershipEvent.requested(personId: personId));
    }
  }

  @override
  Widget build(BuildContext context) {
    final l10n = AppLocalizations.of(context)!;
    return BlocProvider<MembershipBloc>.value(
      value: _bloc,
      child: Scaffold(
        appBar: AppBar(title: Text(l10n.membershipTitle)),
        body: BlocBuilder<MembershipBloc, MembershipState>(
          builder: (context, state) {
            return switch (state) {
              MembershipLoaded(:final membership) => membership == null
                  ? const _NoMembershipView()
                  : _MembershipContent(membership: membership),
              MembershipFailed(:final error) => _MembershipErrorView(
                  error: error,
                  onRetry: _retry,
                ),
              _ => const Center(child: CircularProgressIndicator()),
            };
          },
        ),
      ),
    );
  }
}

class _NoMembershipView extends StatelessWidget {
  const _NoMembershipView();

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
            Icon(Icons.hourglass_empty,
                size: 48, color: theme.colorScheme.outline),
            const SizedBox(height: 12),
            Text(
              l10n.homeNoMembership,
              style: theme.textTheme.titleMedium,
              textAlign: TextAlign.center,
            ),
            const SizedBox(height: 4),
            Text(
              l10n.membershipNoRecordBody,
              style: theme.textTheme.bodyMedium,
              textAlign: TextAlign.center,
            ),
          ],
        ),
      ),
    );
  }
}

class _MembershipErrorView extends StatelessWidget {
  const _MembershipErrorView({required this.error, this.onRetry});

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
                label: Text(l10n.homeRetry),
              ),
            ],
          ],
        ),
      ),
    );
  }
}

class _MembershipContent extends StatelessWidget {
  const _MembershipContent({required this.membership});

  final MembershipDto membership;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final l10n = AppLocalizations.of(context)!;
    final dateFormat = DateFormat.yMMMd(l10n.localeName);

    return ListView(
      padding: const EdgeInsets.all(16),
      children: [
        Card(
          child: Padding(
            padding: const EdgeInsets.all(16),
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Row(
                  children: [
                    Icon(Icons.badge_outlined,
                        color: theme.colorScheme.primary),
                    const SizedBox(width: 8),
                    Expanded(
                      child: Text(
                        membershipStatusLabel(context, membership.status),
                        style: theme.textTheme.titleLarge,
                      ),
                    ),
                  ],
                ),
                const SizedBox(height: 8),
                Text(
                  '${l10n.homeMemberSince}: '
                  '${dateFormat.format(membership.effectiveFrom)}',
                  style: theme.textTheme.bodyMedium,
                ),
                if (membership.effectiveUntil != null) ...[
                  const SizedBox(height: 4),
                  Text(
                    '${l10n.membershipEffectiveUntil}: '
                    '${dateFormat.format(membership.effectiveUntil!)}',
                    style: theme.textTheme.bodyMedium,
                  ),
                ],
                if (membership.withdrawnOn != null) ...[
                  const SizedBox(height: 12),
                  Row(
                    children: [
                      Icon(
                        Icons.event_busy,
                        size: 18,
                        color: theme.colorScheme.error,
                      ),
                      const SizedBox(width: 6),
                      Text(
                        '${l10n.membershipWithdrawnOn}: '
                        '${dateFormat.format(membership.withdrawnOn!)}',
                        style: theme.textTheme.bodyMedium,
                      ),
                    ],
                  ),
                ],
              ],
            ),
          ),
        ),
        if (membership.history.isNotEmpty)
          Card(
            child: Padding(
              padding: const EdgeInsets.all(16),
              child: Column(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Text(
                    l10n.membershipHistory,
                    style: theme.textTheme.titleMedium,
                  ),
                  const SizedBox(height: 8),
                  for (final period in membership.history)
                    ListTile(
                      contentPadding: EdgeInsets.zero,
                      leading: const Icon(Icons.schedule),
                      title: Text(
                        membershipStatusLabel(context, period.status),
                      ),
                      subtitle: Text(
                        _periodWindow(context, l10n, dateFormat, period),
                      ),
                    ),
                ],
              ),
            ),
          ),
      ],
    );
  }

  String _periodWindow(
    BuildContext context,
    AppLocalizations l10n,
    DateFormat dateFormat,
    MembershipPeriodDto period,
  ) {
    final from = dateFormat.format(period.effectiveFrom);
    final until = period.effectiveUntil;
    if (until == null) {
      return '${l10n.membershipPeriodFrom} $from';
    }
    return '$from – ${dateFormat.format(until)}';
  }
}
