import 'package:flutter/material.dart';
import 'package:flutter_bloc/flutter_bloc.dart';
import 'package:go_router/go_router.dart';
import 'package:intl/intl.dart';
import 'package:member_portal/l10n/generated/app_localizations.dart';

import '../../../core/error/app_exception.dart';
import '../../../core/ui/exception_message.dart';
import '../../auth/application/auth_bloc.dart';
import '../../auth/application/auth_event.dart';
import '../application/member_bloc.dart';
import '../application/member_event.dart';
import '../application/member_state.dart';
import '../data/member_dtos.dart';
import '../domain/member_models.dart';
import 'widgets/status_labels.dart';
import 'widgets/unlinked_account_view.dart';

class HomePage extends StatefulWidget {
  const HomePage({super.key});

  @override
  State<HomePage> createState() => _HomePageState();
}

class _HomePageState extends State<HomePage> {
  @override
  void initState() {
    super.initState();
    WidgetsBinding.instance.addPostFrameCallback((_) {
      if (mounted) {
        context.read<MemberBloc>().add(const MemberEvent.homeRequested());
      }
    });
  }

  void _retry() {
    context.read<MemberBloc>().add(const MemberEvent.homeRequested());
  }

  @override
  Widget build(BuildContext context) {
    final l10n = AppLocalizations.of(context)!;
    return Scaffold(
      appBar: AppBar(
        title: Text(l10n.homeTitle),
        actions: [
          IconButton(
            tooltip: l10n.homeLogout,
            icon: const Icon(Icons.logout),
            onPressed: () =>
                context.read<AuthBloc>().add(const AuthEvent.logoutRequested()),
          ),
        ],
      ),
      body: BlocBuilder<MemberBloc, MemberState>(
        builder: (context, state) {
          return switch (state) {
            MemberLoaded(:final data) => _MemberHomeContent(data: data),
            MemberUnlinked() => UnlinkedAccountView(onRetry: _retry),
            MemberSessionExpired() => const _CenteredProgress(),
            MemberForbidden(:final error) =>
              _ErrorView(error: error, onRetry: _retry),
            MemberFailed(:final error) =>
              _ErrorView(error: error, onRetry: _retry),
            MemberProfileFailed(:final error) =>
              _ErrorView(error: error, onRetry: _retry),
            _ => const _CenteredProgress(),
          };
        },
      ),
    );
  }
}

class _CenteredProgress extends StatelessWidget {
  const _CenteredProgress();

  @override
  Widget build(BuildContext context) {
    return const Center(child: CircularProgressIndicator());
  }
}

class _ErrorView extends StatelessWidget {
  const _ErrorView({required this.error, this.onRetry});

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
            Text(
              exceptionMessage(context, error),
              textAlign: TextAlign.center,
            ),
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

class _MemberHomeContent extends StatelessWidget {
  const _MemberHomeContent({required this.data});

  final MemberHomeData data;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final l10n = AppLocalizations.of(context)!;
    final person = data.person;
    final membership = data.membership;

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
    );
  }
}

class _NoMembershipCard extends StatelessWidget {
  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final l10n = AppLocalizations.of(context)!;
    return Card(
      child: Padding(
        padding: const EdgeInsets.all(16),
        child: Row(
          children: [
            Icon(Icons.hourglass_empty, color: theme.colorScheme.outline),
            const SizedBox(width: 12),
            Expanded(child: Text(l10n.homeNoMembership)),
          ],
        ),
      ),
    );
  }
}
