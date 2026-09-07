import 'package:bloc/bloc.dart';

import '../../../core/error/app_exception.dart';
import '../../auth/data/auth_dtos.dart';
import '../../member/domain/member_repository.dart';
import 'account_event.dart';
import 'account_state.dart';

/// Loads the authenticated account overview and read-only security activity,
/// and drives the guarded password-change mutation, for one account page
/// instance.
///
/// Ownership follows the frozen architecture: this bloc is page-scoped
/// (created and closed by the page), never global; `AuthBloc` exclusively owns
/// authentication transitions and owns the centralized session-expiry path the
/// page hands control back to after a successful password change; the backend
/// remains authoritative for every authorization and policy decision.
class AccountBloc extends Bloc<AccountEvent, AccountState> {
  AccountBloc(this._repository) : super(const AccountState.initial()) {
    on<AccountRequested>(_onRequested);
    on<AccountRetryRequested>(_onRetryRequested);
    on<AccountSecurityEventsRequested>(_onSecurityEventsRequested);
    on<AccountPasswordChangeRequested>(_onPasswordChangeRequested);
  }

  final MemberRepository _repository;

  Future<void> _onRequested(
    AccountRequested event,
    Emitter<AccountState> emit,
  ) =>
      _load(emit);

  Future<void> _onRetryRequested(
    AccountRetryRequested event,
    Emitter<AccountState> emit,
  ) =>
      _load(emit);

  Future<void> _load(Emitter<AccountState> emit) async {
    emit(const AccountState.loading());
    final UserAccountDto account;
    try {
      account = await _repository.loadAccount();
    } on AppException catch (error) {
      // The page may have been popped (disposed) while loading; a closed bloc
      // must never emit.
      if (isClosed) return;
      emit(AccountState.failed(_mapError(error)));
      return;
    }
    if (isClosed) return;
    emit(AccountState.loaded(account: account));
    await _loadSecurityEvents(emit, account);
  }

  Future<void> _loadSecurityEvents(
    Emitter<AccountState> emit,
    UserAccountDto account,
  ) async {
    if (isClosed) return;
    emit(AccountState.loaded(
      account: account,
      isSecurityEventsLoading: true,
    ));
    try {
      final events = await _repository.securityEvents();
      if (isClosed) return;
      emit(AccountState.loaded(account: account, securityEvents: events));
    } on AppException catch (error) {
      // Read-only section failure is non-fatal: the overview stays rendered
      // and the section exposes its own retry.
      if (isClosed) return;
      emit(AccountState.loaded(
        account: account,
        securityEventsError: error,
      ));
    }
  }

  Future<void> _onSecurityEventsRequested(
    AccountSecurityEventsRequested event,
    Emitter<AccountState> emit,
  ) async {
    final current = state;
    if (current is! AccountLoaded) return;
    await _loadSecurityEvents(emit, current.account);
  }

  Future<void> _onPasswordChangeRequested(
    AccountPasswordChangeRequested event,
    Emitter<AccountState> emit,
  ) async {
    final current = state;
    if (current is! AccountLoaded) return;
    if (current.passwordStatus is PasswordSubmitting) return;

    emit(current.copyWith(passwordStatus: const PasswordStatus.submitting()));
    try {
      await _repository.changePassword(
        currentPassword: event.currentPassword,
        newPassword: event.newPassword,
      );
      if (isClosed) return;
      emit(current.copyWith(passwordStatus: const PasswordStatus.succeeded()));
    } on AppException catch (error) {
      if (isClosed) return;
      emit(current.copyWith(
        passwordStatus: PasswordStatus.failed(error: error),
      ));
    }
  }

  AppException _mapError(AppException error) {
    if (error is ForbiddenException) {
      return ForbiddenException(
        error.message,
        messageKey: 'account_forbidden',
        code: error.code,
        statusCode: error.statusCode,
      );
    }
    if (error is NotFoundException) {
      return NotFoundException(
        error.message,
        messageKey: 'account_notFound',
        code: error.code,
        statusCode: error.statusCode,
      );
    }
    return error;
  }
}
