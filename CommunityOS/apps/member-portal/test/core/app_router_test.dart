import 'package:flutter_test/flutter_test.dart';
import 'package:member_portal/core/router/app_router.dart';
import 'package:member_portal/features/auth/application/auth_state.dart';
import 'package:member_portal/features/auth/domain/auth_models.dart';

void main() {
  const user = AuthUser(userAccountId: 'u1', email: 'ada@example.org');

  group('authRedirect', () {
    test('keeps the splash while initializing and funnels everything else to it',
        () {
      const state = AuthState.initializing();
      expect(authRedirect('/', state), isNull);
      expect(authRedirect('/login', state), '/');
      expect(authRedirect('/home', state), '/');
      expect(authRedirect('/profile/p1', state), '/');
    });

    test('keeps sign-in and gates every other destination behind it', () {
      const state = AuthState.unauthenticated();
      expect(authRedirect('/login', state), isNull);
      expect(authRedirect('/', state), '/login');
      expect(authRedirect('/mfa', state), '/login');
      expect(authRedirect('/home', state), '/login');
      expect(authRedirect('/profile/p1', state), '/login');
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

    test('keeps sign-in visible on failure states', () {
      const state = AuthState.failure(
        error: AppException('something went wrong'),
      );
      expect(authRedirect('/login', state), isNull);
      expect(authRedirect('/home', state), '/login');
    });
  });
}