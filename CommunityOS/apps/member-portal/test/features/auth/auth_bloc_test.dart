import 'dart:async';

import 'package:flutter_test/flutter_test.dart';
import 'package:member_portal/core/network/refresh_coordinator.dart';
import 'package:member_portal/core/storage/token_storage.dart';
import 'package:member_portal/features/auth/application/auth_bloc.dart';
import 'package:member_portal/features/auth/application/auth_event.dart';
import 'package:member_portal/features/auth/application/auth_state.dart';
import 'package:member_portal/features/auth/domain/auth_models.dart';
import 'package:member_portal/features/auth/domain/auth_repository.dart';
import 'package:mocktail/mocktail.dart';

class _MockRepository extends Mock implements AuthRepository {}

class _MockCoordinator extends Mock implements RefreshCoordinator {}

final _tokens = TokenPair(
  accessToken: 'access-1',
  refreshToken: 'refresh-1',
  expiresAt: DateTime(2030),
);

void main() {
  late _MockRepository repo;
  late _MockCoordinator coordinator;

  setUp(() {
    repo = _MockRepository();
    coordinator = _MockCoordinator();
    when(() => coordinator.onSessionExpired)
        .thenAnswer((_) => const Stream.empty());
  });

  Future<void> pump(AuthBloc bloc) => pumpEventQueue();

  test('appStarted without a stored session emits unauthenticated', () async {
    when(() => repo.restoreSession()).thenAnswer((_) async => null);
    final bloc = AuthBloc(repo, coordinator);
    addTearDown(bloc.close);

    final states = <AuthState>[];
    final sub = bloc.stream.listen(states.add);
    bloc.add(const AuthEvent.appStarted());

    await pump(bloc);

    expect(states.last, isA<AuthUnauthenticated>());
    await sub.cancel();
  });

  test('appStarted restores a valid session into authenticated', () async {
    when(() => repo.restoreSession()).thenAnswer((_) async => _tokens);
    when(() => repo.currentUser()).thenAnswer(
      (_) async => const AuthUser(
        userAccountId: 'u1',
        email: 'ada@example.org',
        status: 'Active',
      ),
    );
    final bloc = AuthBloc(repo, coordinator);
    addTearDown(bloc.close);

    final states = <AuthState>[];
    final sub = bloc.stream.listen(states.add);
    bloc.add(const AuthEvent.appStarted());

    await pump(bloc);

    expect(states.last, isA<AuthAuthenticated>());
    await sub.cancel();
  });

  test('appStarted clears local auth when the restored session is rejected',
      () async {
    when(() => repo.restoreSession()).thenAnswer((_) async => _tokens);
    when(() => repo.currentUser())
        .thenThrow(const UnauthorizedException('session rejected'));
    when(() => repo.clearLocalAuth()).thenAnswer((_) async {});
    final bloc = AuthBloc(repo, coordinator);
    addTearDown(bloc.close);

    final states = <AuthState>[];
    final sub = bloc.stream.listen(states.add);
    bloc.add(const AuthEvent.appStarted());

    await pump(bloc);

    expect(states.last, isA<AuthUnauthenticated>());
    verify(() => repo.clearLocalAuth()).called(1);
    await sub.cancel();
  });

  test('appStarted keeps stored credentials on a transient restore failure',
      () async {
    when(() => repo.restoreSession()).thenAnswer((_) async => _tokens);
    when(() => repo.currentUser())
        .thenThrow(const NetworkException('server unreachable'));
    final bloc = AuthBloc(repo, coordinator);
    addTearDown(bloc.close);

    final states = <AuthState>[];
    final sub = bloc.stream.listen(states.add);
    bloc.add(const AuthEvent.appStarted());

    await pump(bloc);

    expect(states.last, isA<AuthUnauthenticated>());
    verifyNever(() => repo.clearLocalAuth());
    await sub.cancel();
  });

  test('a successful login emits authenticated', () async {
    when(
      () => repo.login(
          email: any(named: 'email'), password: any(named: 'password')),
    ).thenAnswer(
      (_) async => const AuthLoginResult.authenticated(
        user: AuthUser(userAccountId: 'u1', email: 'ada@example.org'),
      ),
    );
    final bloc = AuthBloc(repo, coordinator);
    addTearDown(bloc.close);

    final states = <AuthState>[];
    final sub = bloc.stream.listen(states.add);
    bloc.add(const AuthEvent.loginRequested(
      email: 'ada@example.org',
      password: 'hunter2',
    ));

    await pump(bloc);

    expect(states.last, isA<AuthAuthenticated>());
    await sub.cancel();
  });

  test('a login that requires MFA emits mfaRequired', () async {
    when(
      () => repo.login(
          email: any(named: 'email'), password: any(named: 'password')),
    ).thenAnswer(
      (_) async => const AuthLoginResult.requiresMfa(
          accountId: 'u1', email: 'ada@example.org'),
    );
    final bloc = AuthBloc(repo, coordinator);
    addTearDown(bloc.close);

    final states = <AuthState>[];
    final sub = bloc.stream.listen(states.add);
    bloc.add(const AuthEvent.loginRequested(
      email: 'ada@example.org',
      password: 'hunter2',
    ));

    await pump(bloc);

    final state = states.last;
    expect(state, isA<AuthMfaRequired>());
    expect((state as AuthMfaRequired).email, 'ada@example.org');
    await sub.cancel();
  });

  test('a failed login emits failure with the mapped error', () async {
    when(
      () => repo.login(
          email: any(named: 'email'), password: any(named: 'password')),
    ).thenThrow(
      const UnauthorizedException('bad',
          messageKey: 'login_invalidCredentials'),
    );
    final bloc = AuthBloc(repo, coordinator);
    addTearDown(bloc.close);

    final states = <AuthState>[];
    final sub = bloc.stream.listen(states.add);
    bloc.add(const AuthEvent.loginRequested(
      email: 'ada@example.org',
      password: 'wrong',
    ));

    await pump(bloc);

    final state = states.last;
    expect(state, isA<AuthFailure>());
    expect((state as AuthFailure).error.messageKey, 'login_invalidCredentials');
    await sub.cancel();
  });

  test('an MFA code submission completes the sign-in', () async {
    when(
      () => repo.login(
          email: any(named: 'email'), password: any(named: 'password')),
    ).thenAnswer(
      (_) async => const AuthLoginResult.requiresMfa(
          accountId: 'u1', email: 'ada@example.org'),
    );
    when(
      () => repo.resolveMfa(
        email: any(named: 'email'),
        mfaCode: any(named: 'mfaCode'),
      ),
    ).thenAnswer(
      (_) async => const AuthLoginResult.authenticated(
        user: AuthUser(userAccountId: 'u1', email: 'ada@example.org'),
      ),
    );
    final bloc = AuthBloc(repo, coordinator);
    addTearDown(bloc.close);

    final states = <AuthState>[];
    final sub = bloc.stream.listen(states.add);
    bloc.add(const AuthEvent.loginRequested(
      email: 'ada@example.org',
      password: 'hunter2',
    ));
    await pump(bloc);
    expect(states.last, isA<AuthMfaRequired>());

    bloc.add(const AuthEvent.mfaCodeSubmitted(
      email: 'ada@example.org',
      mfaCode: '123456',
    ));
    await pump(bloc);

    expect(states.last, isA<AuthAuthenticated>());
    await sub.cancel();
  });

  test('an invalid MFA code re-enters mfaRequired with the error', () async {
    when(
      () => repo.login(
          email: any(named: 'email'), password: any(named: 'password')),
    ).thenAnswer(
      (_) async => const AuthLoginResult.requiresMfa(
          accountId: 'u1', email: 'ada@example.org'),
    );
    when(
      () => repo.resolveMfa(
        email: any(named: 'email'),
        mfaCode: any(named: 'mfaCode'),
      ),
    ).thenThrow(
        const UnauthorizedException('bad', messageKey: 'mfa_invalidCode'));
    final bloc = AuthBloc(repo, coordinator);
    addTearDown(bloc.close);

    final states = <AuthState>[];
    final sub = bloc.stream.listen(states.add);
    bloc.add(const AuthEvent.loginRequested(
      email: 'ada@example.org',
      password: 'hunter2',
    ));
    await pump(bloc);

    bloc.add(const AuthEvent.mfaCodeSubmitted(
      email: 'ada@example.org',
      mfaCode: '000000',
    ));
    await pump(bloc);

    final state = states.last;
    expect(state, isA<AuthMfaRequired>());
    expect((state as AuthMfaRequired).error!.messageKey, 'mfa_invalidCode');
    await sub.cancel();
  });

  test('logout clears local auth and emits unauthenticated', () async {
    when(() => repo.logout()).thenAnswer((_) async {});
    final bloc = AuthBloc(repo, coordinator);
    addTearDown(bloc.close);

    final states = <AuthState>[];
    final sub = bloc.stream.listen(states.add);
    bloc.add(const AuthEvent.logoutRequested());

    await pump(bloc);

    expect(states.last, isA<AuthUnauthenticated>());
    verify(() => repo.logout()).called(1);
    await sub.cancel();
  });

  test('a coordinator session-expired event signs the user out', () async {
    final events = StreamController<SessionExpiredEvent>.broadcast();
    when(() => coordinator.onSessionExpired).thenAnswer((_) => events.stream);
    when(() => repo.clearLocalAuth()).thenAnswer((_) async {});
    final bloc = AuthBloc(repo, coordinator);
    addTearDown(bloc.close);

    final states = <AuthState>[];
    final sub = bloc.stream.listen(states.add);
    events.add(const SessionExpiredEvent());
    await pump(bloc);

    expect(states.last, isA<AuthUnauthenticated>());
    verify(() => repo.clearLocalAuth()).called(1);
    await sub.cancel();
    await events.close();
  });
}
