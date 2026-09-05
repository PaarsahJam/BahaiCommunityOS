import 'dart:async';

import 'package:flutter/foundation.dart';
import 'package:go_router/go_router.dart';

import '../../features/auth/application/auth_bloc.dart';
import '../../features/auth/application/auth_state.dart';
import '../../features/auth/presentation/login_page.dart';
import '../../features/auth/presentation/mfa_page.dart';
import '../../features/member/presentation/home_page.dart';
import '../../features/member/presentation/profile_page.dart';
import '../ui/splash_page.dart';

/// Route table for the Member Portal.
///
/// Authorization is not decided by the client: the router only gates navigation
/// on the authentication *state machine* surfaced by [AuthBloc], which itself
/// is driven by backend responses.
class AppRouter {
  static GoRouter build(AuthBloc authBloc) => GoRouter(
        initialLocation: '/',
        refreshListenable: _StreamListenable(authBloc.stream),
        debugLogDiagnostics: kDebugMode,
        redirect: (context, state) => _redirect(state.uri.path, authBloc.state),
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
            ),
          ),
        ],
      );

  static String? _redirect(String path, AuthState state) =>
      authRedirect(path, state);
}

/// Pure redirect decision for the member-portal route table.
///
/// Separated from [AppRouter] so the rules can be unit-tested without a live
/// router: splash while initializing, sign-in gates on unauthenticated /
/// MFA, and member pages only ever render for an authenticated session.
String? authRedirect(String path, AuthState state) {
  return switch (state) {
    AuthInitializing() => path == '/' ? null : '/',
    AuthMfaRequired() => (path == '/login' || path == '/mfa') ? null : '/mfa',
    AuthAuthenticated() =>
      (path == '/' || path == '/login' || path == '/mfa') ? '/home' : null,
    _ => path == '/login' ? null : '/login',
  };
}

/// Bridges an [AuthBloc] state [Stream] to the [Listenable] contract that
/// [GoRouter.refreshListenable] expects so redirects re-evaluate on change.
class _StreamListenable extends ChangeNotifier {
  _StreamListenable(Stream<AuthState> stream) {
    _subscription = stream.listen((_) => notifyListeners());
  }

  late final StreamSubscription<AuthState> _subscription;

  @override
  void dispose() {
    _subscription.cancel();
    super.dispose();
  }
}
