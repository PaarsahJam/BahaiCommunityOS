import 'package:flutter/material.dart';
import 'package:flutter_bloc/flutter_bloc.dart';
import 'package:go_router/go_router.dart';
import 'package:member_portal/l10n/generated/app_localizations.dart';

import '../../../../di/injection.dart';
import '../../application/home_unread_bloc.dart';
import '../../application/home_unread_event.dart';
import '../../application/home_unread_state.dart';
import '../../domain/notification_repository.dart';

/// Home AppBar affordance that opens the Notification Center and shows an
/// authoritative unread indicator.
///
/// The indicator is based only on `GET /my-notifications/unread-count`: no
/// badge at `0`, a count badge above it, and "99+" only when the actual value
/// is known to exceed the threshold. A failed or loading count carries no
/// numeric badge and is never presented as zero.
///
/// Scoped to one Home badge instance (page-scoped, never global). It refreshes
/// the count whenever this route regains focus, so returning from the
/// Notification Center reflects the server's current value without a stale
/// count or a cross-feature event bus.
class HomeNotificationsBadge extends StatefulWidget {
  const HomeNotificationsBadge({super.key, this.createBloc});

  /// Injectable factory for tests; defaults to a bloc backed by the DI
  /// [NotificationRepository] when registered (production).
  final HomeUnreadBloc Function()? createBloc;

  @override
  State<HomeNotificationsBadge> createState() => _HomeNotificationsBadgeState();
}

class _HomeNotificationsBadgeState extends State<HomeNotificationsBadge> {
  late final HomeUnreadBloc? _bloc =
      widget.createBloc?.call() ?? _createDefaultBloc();

  bool _wasCurrent = true;

  /// Production has the repository registered in get_it; isolated widget tests
  /// may not. When unregistered the badge renders without a count rather than
  /// crashing — the Notification Center itself remains independently loadable.
  HomeUnreadBloc? _createDefaultBloc() {
    if (getIt.isRegistered<NotificationRepository>()) {
      return HomeUnreadBloc(getIt<NotificationRepository>());
    }
    return null;
  }

  @override
  void initState() {
    super.initState();
    WidgetsBinding.instance.addPostFrameCallback((_) {
      if (mounted) {
        _bloc?.add(const HomeUnreadEvent.refresh());
      }
    });
  }

  @override
  void didChangeDependencies() {
    super.didChangeDependencies();
    // Refresh when this route regains focus after being covered (for example
    // by the Notification Center), so the badge reflects the authoritative
    // count rather than a stale captured value.
    final isCurrent = ModalRoute.of(context)?.isCurrent ?? true;
    if (isCurrent && !_wasCurrent) {
      _bloc?.add(const HomeUnreadEvent.refresh());
    }
    _wasCurrent = isCurrent;
  }

  @override
  void dispose() {
    _bloc?.close();
    super.dispose();
  }

  void _open() => context.push('/notifications');

  @override
  Widget build(BuildContext context) {
    final bloc = _bloc;
    if (bloc == null) {
      return _build(null, context);
    }
    return BlocProvider<HomeUnreadBloc>.value(
      value: bloc,
      child: BlocBuilder<HomeUnreadBloc, HomeUnreadState>(
        builder: (context, state) {
          return switch (state) {
            HomeUnreadLoaded(:final count) =>
              _build(count > 0 ? count : null, context),
            HomeUnreadInitial() ||
            HomeUnreadLoading() ||
            HomeUnreadFailed() =>
              _build(null, context),
          };
        },
      ),
    );
  }

  /// [unreadCount] is the authoritative positive count, or null to render the
  /// bare affordance (no badge, so a zero count and a loading/failed count are
  /// indistinguishable from nothing being claimed, never presented as zero).
  Widget _build(int? unreadCount, BuildContext context) {
    final l10n = AppLocalizations.of(context)!;
    final showBadge = unreadCount != null && unreadCount > 0;
    final badgeText =
        (unreadCount != null && unreadCount > 99) ? '99+' : '$unreadCount';

    return IconButton(
      key: const Key('open-notifications'),
      tooltip: l10n.notificationsOpen,
      onPressed: _open,
      icon: showBadge
          ? Semantics(
              label: l10n.notificationsUnreadCountSemantic(unreadCount),
              child: ExcludeSemantics(
                child: Badge(
                  isLabelVisible: true,
                  label: Text(badgeText),
                  child: const Icon(Icons.notifications_outlined),
                ),
              ),
            )
          : const Icon(Icons.notifications_outlined),
    );
  }
}
