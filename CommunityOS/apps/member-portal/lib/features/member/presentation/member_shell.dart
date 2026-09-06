import 'package:flutter/material.dart';
import 'package:flutter_bloc/flutter_bloc.dart';
import 'package:member_portal/l10n/generated/app_localizations.dart';

import '../../../di/injection.dart';
import '../../auth/application/auth_bloc.dart';
import '../../auth/application/auth_event.dart';
import '../../../core/error/app_exception.dart';
import '../../../core/ui/exception_message.dart';
import '../application/member_session_bloc.dart';
import '../application/member_session_event.dart';
import '../application/member_session_state.dart';
import '../domain/member_repository.dart';
import 'widgets/unlinked_account_view.dart';

/// Authenticated member-area shell.
///
/// Owns the authenticated-area lifecycle: it creates (and closes) a fresh
/// [MemberSessionBloc] for each authenticated session, bootstraps the member
/// context, and only renders its child pages once the context is ready.
///
/// The shell never performs authorization decisions — the backend remains
/// authoritative. It is responsible for bootstrap states and rendering the
/// authenticated UI.
class MemberShell extends StatefulWidget {
  const MemberShell({super.key, required this.child, this.createSessionBloc});

  final Widget child;

  /// Injectable factory for tests; defaults to a bloc backed by the DI
  /// [MemberRepository].
  final MemberSessionBloc Function()? createSessionBloc;

  @override
  State<MemberShell> createState() => _MemberShellState();
}

class _MemberShellState extends State<MemberShell> {
  late final MemberSessionBloc _sessionBloc = (widget.createSessionBloc ??
      () => MemberSessionBloc(getIt<MemberRepository>()))();

  @override
  void initState() {
    super.initState();
    WidgetsBinding.instance.addPostFrameCallback((_) {
      if (mounted) {
        _sessionBloc.add(const MemberSessionEvent.bootstrapRequested());
      }
    });
  }

  @override
  void dispose() {
    _sessionBloc.close();
    super.dispose();
  }

  void _retry() =>
      _sessionBloc.add(const MemberSessionEvent.bootstrapRequested());

  void _signOut() =>
      context.read<AuthBloc>().add(const AuthEvent.logoutRequested());

  @override
  Widget build(BuildContext context) {
    return BlocProvider<MemberSessionBloc>.value(
      value: _sessionBloc,
      child: BlocBuilder<MemberSessionBloc, MemberSessionState>(
        builder: (context, state) {
          return switch (state) {
            MemberSessionReady() => widget.child,
            MemberSessionUnlinked() => _BootstrapScaffold(
                onSignOut: _signOut,
                child: UnlinkedAccountView(onRetry: _retry),
              ),
            MemberSessionForbidden(:final error) => _BootstrapScaffold(
                onSignOut: _signOut,
                child: _BootstrapErrorView(error: error, onRetry: _retry),
              ),
            MemberSessionFailed(:final error) => _BootstrapScaffold(
                onSignOut: _signOut,
                child: _BootstrapErrorView(error: error, onRetry: _retry),
              ),
            MemberSessionExpired() => _BootstrapScaffold(
                onSignOut: _signOut,
                child: const _BootstrapLoading(),
              ),
            _ => _BootstrapScaffold(
                onSignOut: _signOut,
                child: const _BootstrapLoading(),
              ),
          };
        },
      ),
    );
  }
}

class _BootstrapScaffold extends StatelessWidget {
  const _BootstrapScaffold({required this.child, required this.onSignOut});

  final Widget child;
  final VoidCallback onSignOut;

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
            onPressed: onSignOut,
          ),
        ],
      ),
      body: child,
    );
  }
}

class _BootstrapLoading extends StatelessWidget {
  const _BootstrapLoading();

  @override
  Widget build(BuildContext context) {
    return const Center(child: CircularProgressIndicator());
  }
}

class _BootstrapErrorView extends StatelessWidget {
  const _BootstrapErrorView({required this.error, this.onRetry});

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
