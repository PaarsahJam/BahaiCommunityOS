import 'package:flutter/material.dart';
import 'package:flutter_bloc/flutter_bloc.dart';
import 'package:intl/intl.dart';
import 'package:member_portal/l10n/generated/app_localizations.dart';

import '../../../core/error/app_exception.dart';
import '../../../core/ui/exception_message.dart';
import '../application/member_bloc.dart';
import '../application/member_event.dart';
import '../application/member_state.dart';
import '../data/member_dtos.dart';
import 'widgets/status_labels.dart';

class ProfilePage extends StatefulWidget {
  const ProfilePage({super.key, required this.personId});

  final String personId;

  @override
  State<ProfilePage> createState() => _ProfilePageState();
}

class _ProfilePageState extends State<ProfilePage> {
  @override
  void initState() {
    super.initState();
    WidgetsBinding.instance.addPostFrameCallback((_) {
      if (mounted) {
        context.read<MemberBloc>().add(
              MemberEvent.profileRequested(personId: widget.personId),
            );
      }
    });
  }

  void _retry() {
    context.read<MemberBloc>().add(
          MemberEvent.profileRequested(personId: widget.personId),
        );
  }

  @override
  Widget build(BuildContext context) {
    final l10n = AppLocalizations.of(context)!;
    return Scaffold(
      appBar: AppBar(title: Text(l10n.profileTitle)),
      body: BlocBuilder<MemberBloc, MemberState>(
        builder: (context, state) {
          return switch (state) {
            MemberProfileLoaded(:final detail) =>
              _ProfileContent(detail: detail),
            MemberProfileFailed(:final error) => _ProfileErrorView(
                error: error,
                onRetry: _retry,
              ),
            _ => const Center(child: CircularProgressIndicator()),
          };
        },
      ),
    );
  }
}

class _ProfileErrorView extends StatelessWidget {
  const _ProfileErrorView({required this.error, this.onRetry});

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

class _ProfileContent extends StatelessWidget {
  const _ProfileContent({required this.detail});

  final PersonDetailDto detail;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final l10n = AppLocalizations.of(context)!;

    return ListView(
      padding: const EdgeInsets.all(16),
      children: [
        Card(
          child: Padding(
            padding: const EdgeInsets.all(16),
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text(detail.preferredName,
                    style: theme.textTheme.headlineMedium),
                if (detail.formalName != null &&
                    detail.formalName!.isNotEmpty) ...[
                  const SizedBox(height: 2),
                  Text(detail.formalName!, style: theme.textTheme.bodyMedium),
                ],
                const SizedBox(height: 12),
                Wrap(
                  spacing: 8,
                  runSpacing: 8,
                  children: [
                    Chip(
                      label:
                          Text(membershipStatusLabel(context, detail.status)),
                    ),
                    if (detail.preferredLanguage != null &&
                        detail.preferredLanguage!.isNotEmpty)
                      Chip(
                        label: Text(
                            '${l10n.homePreferredLanguage}: ${detail.preferredLanguage}'),
                      ),
                    if (detail.dateOfBirth != null)
                      Chip(
                        label: Text('${l10n.profileDateOfBirth}: '
                            '${DateFormat.yMMMd(l10n.localeName).format(detail.dateOfBirth!)}'),
                      ),
                  ],
                ),
              ],
            ),
          ),
        ),
        Card(
          child: Padding(
            padding: const EdgeInsets.all(16),
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text(l10n.profileContactHeader,
                    style: theme.textTheme.titleMedium),
                const SizedBox(height: 8),
                if (detail.contactMethods.isEmpty)
                  Text(
                    l10n.profileContactNotVisible,
                    style: theme.textTheme.bodyMedium,
                  )
                else
                  for (final contact in detail.contactMethods)
                    ListTile(
                      contentPadding: EdgeInsets.zero,
                      leading: Icon(_iconFor(contact.type)),
                      title: Text(contact.value),
                      subtitle: Text(contact.type),
                      trailing: contact.isPreferred
                          ? Icon(Icons.star, color: theme.colorScheme.primary)
                          : null,
                    ),
              ],
            ),
          ),
        ),
      ],
    );
  }

  IconData _iconFor(String type) {
    return switch (type) {
      'email' => Icons.email_outlined,
      'phone' => Icons.phone_outlined,
      'mobile' => Icons.smartphone_outlined,
      'address' => Icons.place_outlined,
      'messenger' => Icons.chat_bubble_outline,
      'website' => Icons.public,
      _ => Icons.contact_page_outlined,
    };
  }
}
