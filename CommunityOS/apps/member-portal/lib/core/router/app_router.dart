import 'dart:async';

import 'package:flutter/foundation.dart';
import 'package:go_router/go_router.dart';

import '../../features/auth/application/auth_bloc.dart';
import '../../features/auth/application/auth_state.dart';
import '../../features/auth/presentation/login_page.dart';
import '../../features/auth/presentation/mfa_page.dart';
import '../../features/account/application/account_bloc.dart';
import '../../features/account/application/security_bloc.dart';
import '../../features/account/presentation/account_page.dart';
import '../../features/member/application/member_session_bloc.dart';
import '../../features/member/application/membership_bloc.dart';
import '../../features/member/application/profile_bloc.dart';
import '../../features/member/presentation/home_page.dart';
import '../../features/member/presentation/member_shell.dart';
import '../../features/member/presentation/membership_page.dart';
import '../../features/member/presentation/profile_page.dart';
import '../../features/notifications/application/notification_bloc.dart';
import '../../features/notifications/presentation/notification_center_page.dart';
import '../ui/splash_page.dart';

/// In-memory store for a destination intended while unauthenticated, so the
/// intended route can be honored once sign-in (and MFA if required) completes.
///
/// Only app-local paths are accepted. The store is cleared whenever the auth
/// state becomes unauthenticated (sign-out / session expiry), so a pending
/// destination from one session can never be replayed into a later one.
class PendingRouteStore {
  String? _pending;

  String? get pending => _pending;

  /// Records an intended destination reached while unauthenticated. Ignores
  /// non-local or empty paths; only protected app routes are recorded by the
  /// caller.
  void record(String path) {
    if (path.isEmpty || !path.startsWith('/')) return;
    _pending = path;
  }

  /// Returns and clears the recorded destination, if any.
  String? take() {
    final destination = _pending;
    _pending = null;
    return destination;
  }

  void clear() => _pending = null;
}

/// Route table for the Member Portal.
///
/// Authorization is not decided by the client: the router only gates navigation
/// on the authentication *state machine* surfaced by [AuthBloc], which itself
/// is driven by backend responses. Behind the sign-in gate lies the
/// authenticated member shell ([MemberShell]), which owns the member-context
/// bootstrap and renders the member pages.
class AppRouter {
  static GoRouter build(
    AuthBloc authBloc, {
    MemberSessionBloc Function()? createMemberSession,
    ProfileBloc Function()? createProfile,
    MembershipBloc Function()? createMembership,
    AccountBloc Function()? createAccount,
    SecurityBloc Function()? createSecurity,
    NotificationBloc Function()? createNotifications,
  }) {
    final pending = PendingRouteStore();

    return GoRouter(
      initialLocation: '/',
      refreshListenable: _StreamListenable(authBloc.stream, pending: pending),
      debugLogDiagnostics: kDebugMode,
      redirect: (context, state) =>
          authRedirect(state.uri.path, authBloc.state, pending: pending),
      routes: [
        GoRoute(
          path: '/',
          name: 'splash',
          builder: (context, state) => const SplashPage(),
        ),
        GoRoute(
          path: '/login',
          name: 'login',
          builder: (context, state) => const LoginPage(),
        ),
        GoRoute(
          path: '/mfa',
          name: 'mfa',
          builder: (context, state) => const MfaPage(),
        ),
        ShellRoute(
          builder: (context, state, child) => MemberShell(
            createSessionBloc: createMemberSession,
            child: child,
          ),
          routes: [
            GoRoute(
              path: '/home',
              name: 'home',
              builder: (context, state) => const HomePage(),
            ),
            GoRoute(
              path: '/profile/:personId',
              name: 'profile',
              builder: (context, state) => ProfilePage(
                personId: state.pathParameters['personId']!,
                createProfileBloc: createProfile,
              ),
            ),
            GoRoute(
              path: '/account',
              name: 'account',
              builder: (context, state) => AccountPage(
                createAccountBloc: createAccount,
                createSecurityBloc: createSecurity,
              ),
            ),
            GoRoute(
              path: '/membership',
              name: 'membership',
              builder: (context, state) =>
                  MembershipPage(createMembershipBloc: createMembership),
            ),
            GoRoute(
              path: '/notifications',
              name: 'notifications',
              builder: (context, state) => NotificationCenterPage(
                createBloc: createNotifications,
              ),
            ),
          ],
        ),
      ],
    );
  }
}

/// Pure redirect decision for the member-portal route table.
///
/// Separated from [AppRouter] so the rules can be unit-tested without a live
/// router: splash while initializing, sign-in gates on unauthenticated / MFA,
/// and member pages only ever render for an authenticated session. When a
/// [pending] store is supplied, an unauthenticated attempt on a protected
/// route records the intended destination so sign-in/MFA can restore it.
String? authRedirect(
  String path,
  AuthState state, {
  PendingRouteStore? pending,
}) {
  switch (state) {
    case AuthInitializing():
      return path == '/' ? null : '/';
    case AuthAuthenticating():
      // Transient while credentials are being submitted; never yank the user
      // off the sign-in surface mid-flight.
      return (path == '/login' || path == '/mfa') ? null : '/login';
    case AuthMfaRequired():
      return (path == '/login' || path == '/mfa') ? null : '/mfa';
    case AuthAuthenticated():
      if (path == '/' || path == '/login' || path == '/mfa') {
        return pending?.take() ?? '/home';
      }
      return null;
    case AuthFailure():
    case AuthUnauthenticated():
      if (path == '/login') return null;
      if (_isProtectedPath(path)) pending?.record(path);
      return '/login';
  }
}

bool _isProtectedPath(String path) =>
    path == '/home' ||
    path == '/membership' ||
    path == '/account' ||
    path == '/notifications' ||
    path.startsWith('/profile');

/// Bridges an [AuthBloc] state [Stream] to the [Listenable] contract that
/// [GoRouter.refreshListenable] expects so redirects re-evaluate on change.
/// Also clears any pending destination once a session genuinely ends, so a
/// stale intended route is never replayed across sessions.
class _StreamListenable extends ChangeNotifier {
  _StreamListenable(Stream<AuthState> stream, {PendingRouteStore? pending})
      : _pending = pending {
    _subscription = stream.listen((state) {
      if (state is AuthUnauthenticated) _pending?.clear();
      notifyListeners();
    });
  }

  final PendingRouteStore? _pending;
  late final StreamSubscription<AuthState> _subscription;

  @override
  void dispose() {
    _subscription.cancel();
    super.dispose();
  }
}
