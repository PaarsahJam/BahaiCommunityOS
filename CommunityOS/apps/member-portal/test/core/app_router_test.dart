import 'package:flutter_test/flutter_test.dart';
import 'package:member_portal/core/router/app_router.dart';
import 'package:member_portal/features/auth/application/auth_state.dart';
import 'package:member_portal/features/auth/domain/auth_models.dart';

void main() {
  const user = AuthUser(userAccountId: 'u1', email: 'ada@example.org');

  group('PendingRouteStore', () {
    test('records only app-local paths', () {
      final store = PendingRouteStore();
      expect(store.pending, isNull);

      store.record('/profile/p1');
      expect(store.pending, '/profile/p1');

      store.clear();
      expect(store.pending, isNull);
    });

    test('ignores empty and non-local paths', () {
      final store = PendingRouteStore();
      store.record('');
      store.record('profile/p1');
      store.record('https://evil.example/x');
      expect(store.pending, isNull);
    });

    test('take returns the recorded destination exactly once', () {
      final store = PendingRouteStore();
      store.record('/home');
      expect(store.take(), '/home');
      expect(store.take(), isNull);
      expect(store.pending, isNull);
    });
  });

  group('authRedirect', () {
    test(
        'keeps the splash while initializing and funnels everything else to it',
        () {
      const state = AuthState.initializing();
      expect(authRedirect('/', state), isNull);
      expect(authRedirect('/login', state), '/');
      expect(authRedirect('/home', state), '/');
      expect(authRedirect('/profile/p1', state), '/');
    });

    test('never yanks the user off the sign-in surface while authenticating',
        () {
      const state = AuthState.authenticating();
      expect(authRedirect('/login', state), isNull);
      expect(authRedirect('/mfa', state), isNull);
      expect(authRedirect('/', state), '/login');
      expect(authRedirect('/home', state), '/login');
    });

    test('keeps sign-in and gates every other destination behind it', () {
      const state = AuthState.unauthenticated();
      expect(authRedirect('/login', state), isNull);
      expect(authRedirect('/', state), '/login');
      expect(authRedirect('/mfa', state), '/login');
      expect(authRedirect('/home', state), '/login');
      expect(authRedirect('/profile/p1', state), '/login');
    });

    test(
        'records a protected destination when unauthenticated so sign-in can '
        'restore it', () {
      final store = PendingRouteStore();
      const state = AuthState.unauthenticated();

      expect(authRedirect('/profile/p1', state, pending: store), '/login');
      expect(store.pending, '/profile/p1');
    });

    test('does not record unprotected destinations', () {
      final store = PendingRouteStore();
      const state = AuthState.unauthenticated();

      expect(authRedirect('/login', state, pending: store), isNull);
      expect(store.pending, isNull);
    });

    test('keeps login and mfa open while MFA is required, gates the rest', () {
      const state = AuthState.mfaRequired(email: 'ada@example.org');
      expect(authRedirect('/login', state), isNull);
      expect(authRedirect('/mfa', state), isNull);
      expect(authRedirect('/', state), '/mfa');
      expect(authRedirect('/home', state), '/mfa');
    });

    test(
        'leaves member pages alone and banners auth pages for an authenticated '
        'session', () {
      const state = AuthState.authenticated(user: user);
      expect(authRedirect('/home', state), isNull);
      expect(authRedirect('/profile/p1', state), isNull);
      expect(authRedirect('/login', state), '/home');
      expect(authRedirect('/mfa', state), '/home');
      // Regression: a restored session must leave the splash, not idle on it.
      expect(authRedirect('/', state), '/home');
    });

    test(
        'an authenticated session honors the pending destination exactly '
        'once', () {
      final store = PendingRouteStore();
      store.record('/profile/p1');
      const state = AuthState.authenticated(user: user);

      expect(authRedirect('/login', state, pending: store), '/profile/p1');
      expect(store.pending, isNull);
      expect(authRedirect('/login', state, pending: store), '/home');
    });

    test('keeps sign-in visible on failure states', () {
      const state = AuthState.failure(
        error: AppException('something went wrong'),
      );
      expect(authRedirect('/login', state), isNull);
      expect(authRedirect('/home', state), '/login');
    });
  });
}
