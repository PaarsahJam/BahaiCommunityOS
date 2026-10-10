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
import '../domain/activities_repository.dart';

/// Activities feature, rendered inside the authenticated member shell.
///
/// Displays a list of activities available to the member and allows opening an
/// activity's read-only detail view. This page is scoped to an [ActivitiesBloc]
/// instance it creates and closes itself (via the injected default factory) —
/// never a global bloc — and the backend remains the single authority for data
/// and authorization.
class ActivitiesPage extends StatefulWidget {
  const ActivitiesPage({super.key, this.createBloc});

  /// Injected bloc factory (defaults to get_it) so tests can substitute a
  /// scripted bloc without touching global state.
  final ActivitiesBloc Function()? createBloc;

  @override
  State<ActivitiesPage> createState() => _ActivitiesPageState();
}

class _ActivitiesPageState extends State<ActivitiesPage> {
  late final ActivitiesBloc _bloc = (widget.createBloc ?? _defaultBloc)();

  static ActivitiesBloc _defaultBloc() =>
      ActivitiesBloc(getIt<ActivitiesRepository>());

  @override
  void initState() {
    super.initState();
    _bloc.add(const ActivitiesEvent.requested());
  }

  @override
  void dispose() {
    _bloc.close();
    super.dispose();
  }

  void _reload() => _bloc.add(const ActivitiesEvent.requested());

  @override
  Widget build(BuildContext context) {
    final l10n = AppLocalizations.of(context)!;
    return BlocProvider<ActivitiesBloc>.value(
      value: _bloc,
      child: BlocBuilder<ActivitiesBloc, ActivitiesState>(
        builder: (context, state) {
          return switch (state) {
            ActivitiesDetailLoaded(:final activity) => _ActivitiesDetailView(
                activity: activity,
                onBack: _reload,
              ),
            ActivitiesListLoaded(:final activities) => Scaffold(
                appBar: AppBar(title: Text(l10n.activitiesTitle)),
                body: _ActivitiesListView(activities: activities),
              ),
            ActivitiesFailed(:final error) => Scaffold(
                appBar: AppBar(title: Text(l10n.activitiesTitle)),
                body: _ActivitiesErrorView(error: error, onRetry: _reload),
              ),
            _ => Scaffold(
                appBar: AppBar(title: Text(l10n.activitiesTitle)),
                body: const Center(child: CircularProgressIndicator()),
              ),
          };
        },
      ),
    );
  }
}

class _ActivitiesListView extends StatelessWidget {
  const _ActivitiesListView({required this.activities});

  final List<ActivityDto> activities;

  @override
  Widget build(BuildContext context) {
    final l10n = AppLocalizations.of(context)!;
    final bloc = context.read<ActivitiesBloc>();
    final dateFormat = DateFormat.yMMMd(l10n.localeName);

    if (activities.isEmpty) {
      return _ActivitiesEmptyView(
        onRefresh: () => bloc.add(const ActivitiesEvent.requested()),
      );
    }

    return RefreshIndicator(
      onRefresh: () async {
        bloc.add(const ActivitiesEvent.requested());
        await bloc.stream.firstWhere((state) => state is! ActivitiesLoading);
      },
      child: ListView(
        key: const Key('activities-list'),
        padding: const EdgeInsets.all(16),
        children: [
          for (final activity in activities)
            _ActivitiesListItem(
              activity: activity,
              dateFormat: dateFormat,
              onTap: () => bloc
                  .add(ActivitiesEvent.detailRequested(id: activity.id)),
            ),
        ],
      ),
    );
  }
}

class _ActivitiesEmptyView extends StatelessWidget {
  const _ActivitiesEmptyView({required this.onRefresh});

  final VoidCallback onRefresh;

  @override
  Widget build(BuildContext context) {
    final l10n = AppLocalizations.of(context)!;
    return Center(
      child: Padding(
        padding: const EdgeInsets.all(24),
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: [
            Icon(Icons.event,
                size: 48, color: Theme.of(context).colorScheme.outline),
            const SizedBox(height: 12),
            Text(l10n.activitiesNoActivities,
                style: Theme.of(context).textTheme.titleMedium,
                textAlign: TextAlign.center),
            const SizedBox(height: 16),
            OutlinedButton.icon(
              onPressed: onRefresh,
              icon: const Icon(Icons.refresh),
              label: Text(l10n.activitiesRefresh),
            ),
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
    final l10n = AppLocalizations.of(context)!;
    return Card(
      margin: const EdgeInsets.symmetric(vertical: 8),
      child: ListTile(
        contentPadding: const EdgeInsets.all(12),
        title: Text(activity.title,
            style: theme.textTheme.titleMedium
                ?.copyWith(fontWeight: FontWeight.w500)),
        subtitle: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Text(
              '${l10n.activitiesDate}: '
              '${_dateWindow(dateFormat, activity.startsAt, activity.endsAt)}',
              style: theme.textTheme.bodyMedium,
            ),
            if (_hasText(activity.location)) ...[
              const SizedBox(height: 4),
              Text(
                '${l10n.activitiesLocation}: ${activity.location}',
                style: theme.textTheme.bodySmall,
              ),
            ],
            const SizedBox(height: 4),
            Text(
              '${l10n.activitiesStatus}: ${activity.status}',
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
  const _ActivitiesErrorView({required this.error, this.onRetry});

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

class _ActivitiesDetailView extends StatelessWidget {
  const _ActivitiesDetailView({required this.activity, required this.onBack});

  final ActivityDto activity;
  final VoidCallback onBack;

  @override
  Widget build(BuildContext context) {
    final l10n = AppLocalizations.of(context)!;
    final dateFormat = DateFormat.yMMMd(l10n.localeName);

    return Scaffold(
      appBar: AppBar(
        title: Text('${l10n.activitiesTitle} – ${activity.title}'),
        leading: IconButton(
          icon: const Icon(Icons.arrow_back),
          onPressed: onBack,
        ),
      ),
      body: SingleChildScrollView(
        padding: const EdgeInsets.all(16),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            _DetailRow(l10n.activitiesTitle, activity.title),
            if (_hasText(activity.description)) ...[
              const SizedBox(height: 8),
              _DetailRow(l10n.activitiesDescription, activity.description!),
            ],
            const SizedBox(height: 8),
            _DetailRow(
              l10n.activitiesStartsAt,
              dateFormat.format(activity.startsAt),
            ),
            if (activity.endsAt != null) ...[
              const SizedBox(height: 8),
              _DetailRow(
                l10n.activitiesEndsAt,
                dateFormat.format(activity.endsAt!),
              ),
            ],
            const SizedBox(height: 8),
            _DetailRow(
              l10n.activitiesFormat,
              activity.isOnline ? l10n.activitiesOnline : l10n.activitiesInPerson,
            ),
            if (_hasText(activity.onlineUrl)) ...[
              const SizedBox(height: 8),
              _DetailRow(l10n.activitiesOnlineUrl, activity.onlineUrl!),
            ],
            if (_hasText(activity.location)) ...[
              const SizedBox(height: 8),
              _DetailRow(l10n.activitiesLocation, activity.location!),
            ],
            if (_hasText(activity.organizerPersonId)) ...[
              const SizedBox(height: 8),
              _DetailRow(
                l10n.activitiesOrganizer,
                activity.organizerPersonId!,
              ),
            ],
            if (_hasText(activity.organizationUnitId)) ...[
              const SizedBox(height: 8),
              _DetailRow(
                l10n.activitiesOrganizationUnit,
                activity.organizationUnitId!,
              ),
            ],
            const SizedBox(height: 8),
            _DetailRow(l10n.activitiesStatus, activity.status),
            if (activity.capacity != null) ...[
              const SizedBox(height: 8),
              _DetailRow(
                l10n.activitiesCapacity,
                '${activity.capacity} ${l10n.activitiesOpen}',
              ),
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
          child: Text(label, style: theme.textTheme.titleSmall),
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

bool _hasText(String? value) => value != null && value.trim().isNotEmpty;

String _dateWindow(DateFormat format, DateTime start, DateTime? end) {
  final from = format.format(start);
  return end == null ? from : '$from – ${format.format(end)}';
}
