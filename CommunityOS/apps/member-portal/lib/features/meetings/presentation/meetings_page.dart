import 'package:flutter/material.dart';
import 'package:flutter_bloc/flutter_bloc.dart';
import 'package:intl/intl.dart';
import 'package:member_portal/l10n/generated/app_localizations.dart';

import '../../../core/error/app_exception.dart';
import '../../../core/ui/exception_message.dart';
import '../../../di/injection.dart';
import '../application/meetings_bloc.dart';
import '../application/meetings_event.dart';
import '../application/meetings_state.dart';
import '../data/meetings_dtos.dart';
import '../domain/meetings_models.dart';

/// Meetings feature, rendered inside the authenticated member shell.
///
/// Displays a list of meetings available to the member and allows opening
/// a meeting detail screen. The backend is authoritative for all data;
/// the client only fetches read‑only information.
class MeetingsPage extends StatefulWidget {
  const MeetingsPage({super.key});

  @override
State<MeetingsPage> createState() => _MeetingsPageState();
}

class _MeetingsPageState extends State<MeetingsPage> {
  late final MeetingsBloc _bloc = () => MeetingsBloc(getIt<MeetingsRepository>())();

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
        _bloc.add(const MeetingsEvent.loadMeetings());
      }
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
      appBar: AppBar(title: Text(l10n.meetingsTitle)),
      body: BlocProvider<MeetingsBloc>.value(
        value: _bloc,
        child: BlocBuilder<MeetingsBloc, MeetingsState>(
          builder: (context, state) {
            return switch (state) {
              MeetingsState.initial() => const _MeetingsInitialView(),
              MeetingsState.loading() => const _MeetingsLoadingView(),
              MeetingsState.listSuccess(:final meetings) =>
                  _MeetingsListView(meetings: meetings),
              MeetingsState.detailSuccess(:final meeting) =>
                  _MeetingsDetailView(meeting: meeting),
              MeetingsState.failed(:final error) =>
                  _MeetingsErrorView(error: error),
            };
          },
        ),
      ),
    );
  }

  void _retry() {
    final personId = _personId;
    if (personId != null) {
      context.read<MeetingsBloc>().add(const MeetingsEvent.loadMeetings());
    }
  }

  @override
  void endRefresh() {
    // No-op for meetings; refresh is driven by the initial load event.
  }
}

class _MeetingsInitialView extends StatelessWidget {
  const _MeetingsInitialView();

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
            Text(l10n.meetingsLoading,
                style: Theme.of(context).textTheme.titleMedium),
          ],
        ),
      ),
    );
  }
}

class _MeetingsLoadingView extends StatelessWidget {
  const _MeetingsLoadingView();

  @override
  Widget build(BuildContext context) {
    return const Center(child: CircularProgressIndicator());
  }
}

class _MeetingsListView extends StatelessWidget {
  const _MeetingsListView({required this.meetings});

  final List<MeetingDto> meetings;

  @override
  Widget build(BuildContext context) {
    final l10n = AppLocalizations.of(context)!;
    final dateFormat = DateFormat.yMMMd(l10n.localeName);

    if (meetings.isEmpty) {
      return _MeetingsEmptyView(l10n: l10n);
    }

    return ListView(
      padding: const EdgeInsets.all(16),
      children: [
        for (final meeting in meetings)
          _MeetingsListItem(
            meeting: meeting,
            dateFormat: dateFormat,
            onTap: () {
              context.read<MeetingsBloc>().add(MeetingsEvent.loadDetail(meeting.id));
            },
          ),
      ],
    );
  }
}

class _MeetingsEmptyView extends StatelessWidget {
  const _MeetingsEmptyView({required this.l10n});

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
            Text(l10n.meetingsNoMeetings,
                style: Theme.of(context).textTheme.titleMedium,
                textAlign: TextAlign.center),
          ],
        ),
      ),
    );
  }
}

class _MeetingsListItem extends StatelessWidget {
  const _MeetingsListItem({
    required this.meeting,
    required this.dateFormat,
    required this.onTap,
  });

  final MeetingDto meeting;
  final DateFormat dateFormat;
  final VoidCallback onTap;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    return Card(
      margin: const EdgeInsets.symmetric(vertical: 8),
      child: ListTile(
        contentPadding: const EdgeInsets.all(12),
        title: Text(meeting.title,
            style: theme.textTheme.titleMedium?.copyWith(
              fontWeight: FontWeight.w500,
            )),
        subtitle: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Text(
              '${l10n.meetingsDate}: ${dateFormat.format(meeting.startsAt)} – '
                  '${dateFormat.format(meeting.endsAt)}',
              style: theme.textTheme.bodyMedium,
            ),
            if (meeting.location.isNotEmpty) ...[
              const SizedBox(height: 4),
              Text(
                '${l10n.meetingsLocation}: ${meeting.location}',
                style: theme.textTheme.bodySmall,
              ),
            ],
            const SizedBox(height: 4),
            Text(
              '${l10n.meetingsStatus}: ${meeting.status',
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

class _MeetingsErrorView extends StatelessWidget {
  const _MeetingsErrorView({required this.error});

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
                context.read<MeetingsBloc>().add(const MeetingsEvent.loadMeetings());
              },
              child: Text(l10n.refresh),
            ),
          ],
        ),
      ),
    );
  }
}

class _MeetingsDetailView extends StatelessWidget {
  const _MeetingsDetailView({required this.meeting});

  final MeetingDto meeting;

  @override
  Widget build(BuildContext context) {
    final l10n = AppLocalizations.of(context)!;
    final dateFormat = DateFormat.yMMMd(l10n.localeName);

    return Scaffold(
      appBar: AppBar(
        title: Text('${l10n.meetingsTitle} – ${meeting.title}'),
        leading: IconButton(
          icon: const Icon(Icons.arrow_back),
          onPressed: () {
            context.read<MeetingsBloc>().add(const MeetingsEvent.loadMeetings());
          },
        ),
      ),
      body: SingleChildScrollView(
        padding: const EdgeInsets.all(16),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            _DetailRow(l10n.meetingsTitle, meeting.title),
            const SizedBox(height: 8),
            _DetailRow(l10n.meetingsDescription, meeting.description),
            const SizedBox(height: 8),
            _DetailRow(l10n.meetingsStartsAt, dateFormat.format(meeting.startsAt)),
            _DetailRow(l10n.meetingsEndsAt, dateFormat.format(meeting.endsAt)),
            if (meeting.timeZone.isNotEmpty) ...[
              const SizedBox(height: 4),
              _DetailRow(l10n.meetingsTimeZone, meeting.timeZone),
            ],
            if (meeting.location.isNotEmpty) ...[
              const SizedBox(height: 4),
              _DetailRow(l10n.meetingsLocation, meeting.location),
            ],
            const SizedBox(height: 8),
            _DetailRow(l10n.meetingsOrganizer, meeting.organizerPersonId),
            const SizedBox(height: 8),
            _DetailRow(l10n.meetingsOrganizationUnit, meeting.organizationUnitId),
            const SizedBox(height: 8),
            _DetailRow(l10n.meetingsStatus, meeting.status),
            const SizedBox(height: 16),
            if (meeting.registrationOpen != null)
              _DetailRow(l10n.meetingsCapacity,
                  '${meeting.capacity ?? '–'} ${meeting.registrationOpen! ? '${l10n.meetingsOpen}' : '${l10n.meetingsClosed}'}'),
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