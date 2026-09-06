import 'package:bloc/bloc.dart';

import '../../../core/error/app_exception.dart';
import '../domain/member_repository.dart';
import 'membership_event.dart';
import 'membership_state.dart';

/// Loads the authenticated member's authoritative membership record for one
/// membership page instance.
///
/// Errors are typed and keyed for localization:
/// * 404 → [NotFoundException] (`membership_notFound`)
/// * 403 → [ForbiddenException] (`membership_forbidden`)
/// * every other failure (401, timeout, network, 5xx) propagates as-is — 401
///   follows the existing session-expiry machinery and is never converted into
///   a "no membership" state.
class MembershipBloc extends Bloc<MembershipEvent, MembershipState> {
  MembershipBloc(this._repository) : super(const MembershipState.initial()) {
    on<MembershipRequested>(_onRequested);
  }

  final MemberRepository _repository;

  Future<void> _onRequested(
    MembershipRequested event,
    Emitter<MembershipState> emit,
  ) async {
    emit(const MembershipState.loading());
    try {
      final membership = await _repository.getMembership(event.personId);
      // The page may have been popped (disposed) while loading; a closed bloc
      // must never emit.
      if (isClosed) return;
      emit(MembershipState.loaded(membership));
    } on AppException catch (error) {
      if (isClosed) return;
      emit(MembershipState.failed(_mapError(error)));
    }
  }

  AppException _mapError(AppException error) {
    if (error is NotFoundException) {
      return NotFoundException(
        error.message,
        messageKey: 'membership_notFound',
        code: error.code,
        statusCode: error.statusCode,
      );
    }
    if (error is ForbiddenException) {
      return ForbiddenException(
        error.message,
        messageKey: 'membership_forbidden',
        code: error.code,
        statusCode: error.statusCode,
      );
    }
    return error;
  }
}
