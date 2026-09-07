import 'package:flutter/material.dart';
import 'package:flutter_bloc/flutter_bloc.dart';
import 'package:go_router/go_router.dart';
import 'package:intl/intl.dart';
import 'package:member_portal/l10n/generated/app_localizations.dart';

import '../../auth/application/auth_bloc.dart';
import '../../auth/application/auth_event.dart';
import '../application/member_session_bloc.dart';
import '../application/member_session_state.dart';
import '../data/member_dtos.dart';
import '../domain/member_models.dart';
import 'widgets/status_labels.dart';

/// Member home screen, rendered inside the authenticated member shell.
///
/// The shell gates rendering on a ready [MemberSessionState], so this page
/// only ever presents the bootstrap context; bootstrap/unlinked/forbidden/
/// failed states are the shell's responsibility.
class HomePage extends StatelessWidget {
  const HomePage({super.key});

  @override
  Widget build(BuildContext context) {
    final l10n = AppLocalizations.of(context)!;
    final memberContext = context.select<MemberSessionBloc, MemberContext?>(
      (bloc) => switch (bloc.state) {
        MemberSessionReady(:final context) => context,
        _ => null,
      },
    );

    return Scaffold(
      appBar: AppBar(
        title: Text(l10n.homeTitle),
        actions: [
          IconButton(
            tooltip: l10n.accountTitle,
            icon: const Icon(Icons.manage_accounts_outlined),
            onPressed: () => context.push('/account'),
          ),
          IconButton(
            tooltip: l10n.homeLogout,
            icon: const Icon(Icons.logout),
            onPressed: () =>
                context.read<AuthBloc>().add(const AuthEvent.logoutRequested()),
          ),
        ],
      ),
      body: memberContext == null
          ? const Center(child: CircularProgressIndicator())
          : _MemberHomeContent(memberContext: memberContext),
    );
  }
}

class _MemberHomeContent extends StatelessWidget {
  const _MemberHomeContent({required this.memberContext});

  final MemberContext memberContext;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final l10n = AppLocalizations.of(context)!;
    final person = memberContext.person;
    final membership = memberContext.membership;

    return ListView(
      padding: const EdgeInsets.all(16),
      children: [
        Card(
          child: Padding(
            padding: const EdgeInsets.all(16),
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text(
                  l10n.homeWelcome,
                  style: theme.textTheme.bodyMedium,
                ),
                const SizedBox(height: 4),
                Text(
                  person.preferredName,
                  style: theme.textTheme.headlineMedium,
                ),
                if (person.formalName != null &&
                    person.formalName!.isNotEmpty) ...[
                  const SizedBox(height: 2),
                  Text(person.formalName!, style: theme.textTheme.bodyMedium),
                ],
                const SizedBox(height: 12),
                Wrap(
                  spacing: 8,
                  runSpacing: 8,
                  children: [
                    Chip(
                      label:
                          Text(membershipStatusLabel(context, person.status)),
                      avatar: const Icon(Icons.badge_outlined),
                    ),
                    if (person.preferredLanguage != null &&
                        person.preferredLanguage!.isNotEmpty)
                      Chip(
                        label: Text(
                            '${l10n.homePreferredLanguage}: ${person.preferredLanguage}'),
                        avatar: const Icon(Icons.language),
                      ),
                  ],
                ),
                const SizedBox(height: 8),
                Align(
                  alignment: Alignment.centerRight,
                  child: TextButton.icon(
                    onPressed: () => context.push('/profile/${person.id}'),
                    icon: const Icon(Icons.chevron_right),
                    label: Text(l10n.profileTitle),
                  ),
                ),
              ],
            ),
          ),
        ),
        if (membership != null)
          _MembershipCard(membership: membership)
        else
          _NoMembershipCard(),
      ],
    );
  }
}

class _MembershipCard extends StatelessWidget {
  const _MembershipCard({required this.membership});

  final MembershipDto membership;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final l10n = AppLocalizations.of(context)!;
    return Card(
      clipBehavior: Clip.antiAlias,
      child: InkWell(
        onTap: () => context.push('/membership'),
        child: Padding(
          padding: const EdgeInsets.all(16),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Row(
                children: [
                  Text(l10n.homeMembershipStatus,
                      style: theme.textTheme.titleMedium),
                  const Spacer(),
                  Chip(
                    label: Text(
                      membershipStatusLabel(context, membership.status),
                      style: theme.textTheme.labelMedium,
                    ),
                  ),
                  const SizedBox(width: 4),
                  Icon(Icons.chevron_right, color: theme.colorScheme.outline),
                ],
              ),
              const SizedBox(height: 8),
              Text(
                '${l10n.homeMemberSince}: '
                '${DateFormat.yMMMd(l10n.localeName).format(membership.effectiveFrom)}',
                style: theme.textTheme.bodyMedium,
              ),
            ],
          ),
        ),
      ),
    );
  }
}

class _NoMembershipCard extends StatelessWidget {
  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final l10n = AppLocalizations.of(context)!;
    return Card(
      clipBehavior: Clip.antiAlias,
      child: InkWell(
        onTap: () => context.push('/membership'),
        child: Padding(
          padding: const EdgeInsets.all(16),
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Row(
                children: [
                  Text(l10n.homeMembershipStatus,
                      style: theme.textTheme.titleMedium),
                  const Spacer(),
                  Icon(Icons.chevron_right, color: theme.colorScheme.outline),
                ],
              ),
              const SizedBox(height: 8),
              Row(
                children: [
                  Icon(Icons.hourglass_empty, color: theme.colorScheme.outline),
                  const SizedBox(width: 12),
                  Expanded(child: Text(l10n.homeNoMembership)),
                ],
              ),
            ],
          ),
        ),
      ),
    );
  }
}
