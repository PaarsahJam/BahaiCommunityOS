import 'package:flutter/material.dart';
import 'package:flutter_bloc/flutter_bloc.dart';
import 'package:intl/intl.dart';
import 'package:member_portal/l10n/generated/app_localizations.dart';

import '../../../core/error/app_exception.dart';
import '../../../core/ui/exception_message.dart';
import '../../../di/injection.dart';
import '../../member/application/member_session_bloc.dart';
import '../../member/application/member_session_state.dart';
import '../bloc/meetings_bloc.dart';
import '../data/meetings_dtos.dart';
import '../domain/meetings_event.dart';
import '../domain/meetings_state.dart';

/// Meetings feature, rendered inside the authenticated member shell.
///
/// Displays a list of meetings available to the member and allows opening
/// a meeting detail screen. The backend is authoritative for all data;
/// the client only fetches read-only information.
class MeetingsPage extends StatefulWidget {
  const MeetingsPage({super.key});

  @override
  State<MeetingsPage> createState() => _MeetingsPageState();
}

class _MeetingsPageState extends State<MeetingsPage> {
  late final MeetingsBloc _bloc = getIt<MeetingsBloc>();

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
              MeetingsStateInitial() => const _MeetingsInitialView(),
              MeetingsStateLoading() => const _MeetingsLoadingView(),
              MeetingsStateListSuccess(:final meetings) =>
                _MeetingsListView(meetings: meetings),
              MeetingsStateDetailSuccess(:final meeting) =>
                _MeetingsDetailView(meeting: meeting),
              MeetingsStateFailed(:final error) =>
                _MeetingsErrorView(error: error),
            };
          },
        ),
      ),
    );
  }
}

/// Formats a start/end range into one display string when at least the start is
/// available; otherwise [null].
String? _formatDateRange(
  DateFormat dateFormat,
  DateTime? startsAt,
  DateTime? endsAt,
) {
  if (startsAt == null) return null;
  final start = dateFormat.format(startsAt);
  if (endsAt == null) return start;
  return '$start – ${dateFormat.format(endsAt)}';
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
            Icon(Icons.event,
                size: 48, color: Theme.of(context).colorScheme.outline),
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
              context
                  .read<MeetingsBloc>()
                  .add(MeetingsEvent.loadDetail(meeting.id));
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
            Icon(Icons.event,
                size: 48, color: Theme.of(context).colorScheme.outline),
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
    final l10n = AppLocalizations.of(context)!;
    final location = meeting.location;
    final dateRange =
        _formatDateRange(dateFormat, meeting.startsAt, meeting.endsAt);

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
            if (dateRange != null)
              Text(
                '${l10n.meetingsDate}: $dateRange',
                style: theme.textTheme.bodyMedium,
              ),
            if (location != null && location.isNotEmpty) ...[
              const SizedBox(height: 4),
              Text(
                '${l10n.meetingsLocation}: $location',
                style: theme.textTheme.bodySmall,
              ),
            ],
            const SizedBox(height: 4),
            Text(
              '${l10n.meetingsStatus}: ${meeting.status}',
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
                context
                    .read<MeetingsBloc>()
                    .add(const MeetingsEvent.loadMeetings());
              },
              child: Text(l10n.homeRetry),
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
    final description = meeting.description;
    final startsAt = meeting.startsAt;
    final endsAt = meeting.endsAt;
    final timeZone = meeting.timeZone;
    final location = meeting.location;
    final organizer = meeting.organizerPersonId;
    final organizationUnit = meeting.organizationUnitId;
    final registrationOpen = meeting.registrationOpen;

    return Scaffold(
      appBar: AppBar(
        title: Text('${l10n.meetingsTitle} – ${meeting.title}'),
        leading: IconButton(
          icon: const Icon(Icons.arrow_back),
          onPressed: () {
            context
                .read<MeetingsBloc>()
                .add(const MeetingsEvent.loadMeetings());
          },
        ),
      ),
      body: SingleChildScrollView(
        padding: const EdgeInsets.all(16),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            if (description != null && description.trim().isNotEmpty) ...[
              _DetailRow(l10n.meetingsDescription, description),
              const SizedBox(height: 8),
            ],
            if (startsAt != null) ...[
              _DetailRow(l10n.meetingsStartsAt, dateFormat.format(startsAt)),
              const SizedBox(height: 8),
            ],
            if (endsAt != null) ...[
              _DetailRow(l10n.meetingsEndsAt, dateFormat.format(endsAt)),
              const SizedBox(height: 8),
            ],
            if (timeZone != null && timeZone.isNotEmpty) ...[
              _DetailRow(l10n.meetingsTimeZone, timeZone),
              const SizedBox(height: 8),
            ],
            if (location != null && location.isNotEmpty) ...[
              _DetailRow(l10n.meetingsLocation, location),
              const SizedBox(height: 8),
            ],
            if (organizer != null && organizer.isNotEmpty) ...[
              _DetailRow(l10n.meetingsOrganizer, organizer),
              const SizedBox(height: 8),
            ],
            if (organizationUnit != null && organizationUnit.isNotEmpty) ...[
              _DetailRow(l10n.meetingsOrganizationUnit, organizationUnit),
              const SizedBox(height: 8),
            ],
            _DetailRow(l10n.meetingsStatus, meeting.status),
            if (registrationOpen != null) ...[
              const SizedBox(height: 8),
              _DetailRow(
                l10n.meetingsCapacity,
                '${meeting.capacity ?? '–'} '
                '${registrationOpen ? l10n.meetingsOpen : l10n.meetingsClosed}',
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