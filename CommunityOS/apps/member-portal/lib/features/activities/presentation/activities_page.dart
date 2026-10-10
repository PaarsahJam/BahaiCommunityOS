import 'package:flutter/material.dart';
import 'package:flutter_bloc/flutter_bloc.dart';
import 'package:intl/intl.dart';
import 'package:member_portal/l10n/generated/app_localizations.dart';

import '../../../core/error/app_exception.dart';
import '../../../core/ui/exception_message.dart';
import '../../../di/injection.dart';
import '../application/activities_bloc.dart';
import '../application/activities_event.dart';
import '../application/activities_state.dart';
import '../data/activities_dtos.dart';
import '../domain/activities_models.dart';

/// Activities feature, rendered inside the authenticated member shell.
///
/// Displays a list of activities available to the member and allows opening
/// an activity detail screen. The backend is authoritative for all data;
/// the client only fetches read-only information.
class ActivitiesPage extends StatefulWidget {
  const ActivitiesPage({super.key});

  @override
  State<ActivitiesPage> createState() => _ActivitiesPageState();
}

class _ActivitiesPageState extends State<ActivitiesPage> {
  late final ActivitiesBloc _bloc = () => ActivitiesBloc(getIt<ActivitiesRepository>())();

  @override
  void initState() {
    super.initState();
    WidgetsBinding.instance.addPostFrameCallback((_) {
      if (!mounted) return;
      _bloc.add(const ActivitiesEvent.loadActivities());
    });
  }

  @override
  void dispose() {
    _bloc.close();
    super.dispose();
  }

  @override
  Widget build(BuildContext context) {
    final l10n = AppLocalizations.of(context)!;
    return Scaffold(
      appBar: AppBar(title: Text(l10n.activitiesTitle)),
      body: BlocProvider<ActivitiesBloc>.value(
        value: _bloc,
        child: BlocBuilder<ActivitiesBloc, ActivitiesState>(
          builder: (context, state) {
            return switch (state) {
              ActivitiesState.initial() => const _ActivitiesInitialView(),
              ActivitiesState.loading() => const _ActivitiesLoadingView(),
              ActivitiesState.listSuccess(:final activities) =>
                  _ActivitiesListView(activities: activities),
              ActivitiesState.detailSuccess(:final activity) =>
                  _ActivitiesDetailView(activity: activity),
              ActivitiesState.failed(:final error) =>
                  _ActivitiesErrorView(error: error),
            };
          },
        ),
      ),
    );
  }

  @override
  void endRefresh() {
    // No-op for activities; refresh is driven by the initial load event.
  }
}

class _ActivitiesInitialView extends StatelessWidget {
  const _ActivitiesInitialView();

  @override
  Widget build(BuildContext context) {
    final l10n = AppLocalizations.of(context)!;
    return Center(
      child: Padding(
        padding: const EdgeInsets.all(24),
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: [
            Icon(Icons.event, size: 48, color: Theme.of(context).colorScheme.outline),
            const SizedBox(height: 12),
            Text(l10n.activitiesLoading,
                style: Theme.of(context).textTheme.titleMedium),
          ],
        ),
      ),
    );
  }
}

class _ActivitiesLoadingView extends StatelessWidget {
  const _ActivitiesLoadingView();

  @override
  Widget build(BuildContext context) {
    return const Center(child: CircularProgressIndicator());
  }
}

class _ActivitiesListView extends StatelessWidget {
  const _ActivitiesListView({required this.activities});

  final List<ActivityDto> activities;

  @override
  Widget build(BuildContext context) {
    final l10n = AppLocalizations.of(context)!;
    final dateFormat = DateFormat.yMMMd(l10n.localeName);

    if (activities.isEmpty) {
      return _ActivitiesEmptyView(l10n: l10n);
    }

    return ListView(
      padding: const EdgeInsets.all(16),
      children: [
        for (final activity in activities)
          _ActivitiesListItem(
            activity: activity,
            dateFormat: dateFormat,
            onTap: () {
              context.read<ActivitiesBloc>().add(ActivitiesEvent.detail(activity.id));
            },
          ),
      ],
    );
  }
}

class _ActivitiesEmptyView extends StatelessWidget {
  const _ActivitiesEmptyView({required this.l10n});

  final AppLocalizations l10n;

  @override
  Widget build(BuildContext context) {
    return Center(
      child: Padding(
        padding: const EdgeInsets.all(24),
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: [
            Icon(Icons.event, size: 48, color: Theme.of(context).colorScheme.outline),
            const SizedBox(height: 12),
            Text(l10n.activitiesNoActivities,
                style: Theme.of(context).textTheme.titleMedium,
                textAlign: TextAlign.center),
          ],
        ),
      ),
    );
  }
}

class _ActivitiesListItem extends StatelessWidget {
  const _ActivitiesListItem({
    required this.activity,
    required this.dateFormat,
    required this.onTap,
  });

  final ActivityDto activity;
  final DateFormat dateFormat;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    return Card(
      margin: const EdgeInsets.symmetric(vertical: 8),
      child: ListTile(
        contentPadding: const EdgeInsets.all(12),
        title: Text(activity.title,
            style: theme.textTheme.titleMedium?.copyWith(
              fontWeight: FontWeight.w500,
            )),
        subtitle: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Text(
              '${l10n.activitiesDate}: ${dateFormat.format(activity.startsAt)} – '
                  '${dateFormat.format(activity.endsAt)}',
              style: theme.textTheme.bodyMedium,
            ),
            if (activity.location.isNotEmpty) ...[
              const SizedBox(height: 4),
              Text(
                '${l10n.activitiesLocation}: ${activity.location}',
                style: theme.textTheme.bodySmall,
              ),
            ],
            const SizedBox(height: 4),
            Text(
              '${l10n.activitiesStatus}: ${activity.status',
              style: theme.textTheme.bodySmall,
            ),
          ],
        ),
        trailing: Icon(Icons.arrow_forward_ios,
            size: 16, color: theme.colorScheme.outline),
        onTap: onTap,
      ),
    );
  }
}

class _ActivitiesErrorView extends StatelessWidget {
  const _ActivitiesErrorView({required this.error});

  final AppException error;

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
            const SizedBox(height: 16),
            OutlinedButton(
              onPressed: () {
                context.read<ActivitiesBloc>().add(const ActivitiesEvent.loadActivities());
              },
              child: Text(l10n.refresh),
            ),
          ],
        ),
      ),
    );
  }
}

class _ActivitiesDetailView extends StatelessWidget {
  const _ActivitiesDetailView({required this.activity});

  final ActivityDto activity;

  @override
  Widget build(BuildContext context) {
    final l10n = AppLocalizations.of(context)!;
    final dateFormat = DateFormat.yMMMd(l10n.localeName);

    return Scaffold(
      appBar: AppBar(
        title: Text('${l10n.activitiesTitle} – ${activity.title}'),
        leading: IconButton(
          icon: const Icon(Icons.arrow_back),
          onPressed: () {
            context.read<ActivitiesBloc>().add(const ActivitiesEvent.loadActivities());
          },
        ),
      ),
      body: SingleChildScrollView(
        padding: const EdgeInsets.all(16),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            _DetailRow(l10n.activitiesTitle, activity.title),
            const SizedBox(height: 8),
            _DetailRow(l10n.activitiesDescription, activity.description),
            const SizedBox(height: 8),
            _DetailRow(l10n.activitiesStartsAt, dateFormat.format(activity.startsAt)),
            _DetailRow(l10n.activitiesEndsAt, dateFormat.format(activity.endsAt)),
            if (activity.isOnline != null) ...[
              const SizedBox(height: 4),
              _DetailRow(l10n.activitiesOnline, activity.isOnline! ? 'Online' : 'In person'),
            ],
            if (activity.onlineUrl.isNotEmpty) ...[
              const SizedBox(height: 4),
              _DetailRow(l10n.activitiesOnlineUrl, activity.onlineUrl),
            ],
            if (activity.location.isNotEmpty) ...[
              const SizedBox(height: 4),
              _DetailRow(l10n.activitiesLocation, activity.location),
            ],
            const SizedBox(height: 8),
            _DetailRow(l10n.activitiesOrganizer, activity.organizerPersonId),
            const SizedBox(height: 8),
            _DetailRow(l10n.activitiesOrganizationUnit, activity.organizationUnitId),
            const SizedBox(height: 8),
            _DetailRow(l10n.activitiesStatus, activity.status),
            if (activity.capacity != null) ...[
              const SizedBox(height: 4),
              _DetailRow(l10n.activitiesCapacity,
                  '${activity.capacity ?? '–'} ${activity.isOnline! ? '' : '${l10n.activitiesOpen}'}'),
            ],
          ],
        ),
      ),
    );
  }
}

class _DetailRow extends StatelessWidget {
  const _DetailRow(this.label, this.value);

  final String label;
  final String value;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    return Row(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Expanded(
          child: Text(
            label,
            style: theme.textTheme.titleSmall,
          ),
        ),
        const SizedBox(width: 4),
        Expanded(
          child: Text(
            value.isEmpty ? '–' : value,
            style: theme.textTheme.bodyMedium,
          ),
        ),
      ],
    );
  }
}