import 'package:bloc/bloc.dart';

import '../../../core/error/app_exception.dart';
import '../domain/member_repository.dart';
import 'profile_event.dart';
import 'profile_state.dart';

/// Loads a person profile detail for one profile page instance.
///
/// Errors are typed and keyed for localization:
/// * 404 → [NotFoundException] (`profile_notFound`)
/// * 403 → [ForbiddenException] (`profile_forbidden`)
/// * everything else propagates as-is.
class ProfileBloc extends Bloc<ProfileEvent, ProfileState> {
  ProfileBloc(this._repository) : super(const ProfileState.initial()) {
    on<ProfileRequested>(_onRequested);
  }

  final MemberRepository _repository;

  Future<void> _onRequested(
    ProfileRequested event,
    Emitter<ProfileState> emit,
  ) async {
    emit(const ProfileState.loading());
    try {
      final detail = await _repository.personDetail(event.personId);
      // The page may have been popped (disposed) while loading; a closed bloc
      // must never emit.
      if (isClosed) return;
      emit(ProfileState.loaded(detail));
    } on AppException catch (error) {
      if (isClosed) return;
      emit(ProfileState.failed(_mapError(error)));
    }
  }

  AppException _mapError(AppException error) {
    if (error is NotFoundException) {
      return NotFoundException(
        error.message,
        messageKey: 'profile_notFound',
        code: error.code,
        statusCode: error.statusCode,
      );
    }
    if (error is ForbiddenException) {
      return ForbiddenException(
        error.message,
        messageKey: 'profile_forbidden',
        code: error.code,
        statusCode: error.statusCode,
      );
    }
    return error;
  }
}
