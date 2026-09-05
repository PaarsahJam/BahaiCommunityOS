import 'package:bloc/bloc.dart';
import 'package:injectable/injectable.dart';

import '../../../core/error/app_exception.dart';
import '../domain/member_models.dart';
import '../domain/member_repository.dart';
import 'member_event.dart';
import 'member_state.dart';

@LazySingleton()
class MemberBloc extends Bloc<MemberEvent, MemberState> {
  MemberBloc(this._repository) : super(const MemberState.initial()) {
    on<MemberHomeRequested>(_onHomeRequested);
    on<MemberProfileRequested>(_onProfileRequested);
  }

  final MemberRepository _repository;

  Future<void> _onHomeRequested(
    MemberHomeRequested event,
    Emitter<MemberState> emit,
  ) async {
    emit(const MemberState.loading());
    final result = await _repository.loadMemberHome();
    switch (result) {
      case MemberHomeResolved(:final person, :final membership):
        emit(
          MemberState.loaded(
            MemberHomeData(person: person, membership: membership),
          ),
        );
      case MemberHomeUnlinked():
        emit(const MemberState.unlinked());
      case MemberHomeUnauthenticated():
        emit(const MemberState.sessionExpired());
      case MemberHomeForbidden(:final error):
        emit(MemberState.forbidden(error));
      case MemberHomeFailed(:final error):
        emit(MemberState.failed(error));
    }
  }

  Future<void> _onProfileRequested(
    MemberProfileRequested event,
    Emitter<MemberState> emit,
  ) async {
    emit(const MemberState.profileLoading());
    try {
      final detail = await _repository.personDetail(event.personId);
      emit(MemberState.profileLoaded(detail));
    } on AppException catch (error) {
      final mapped = _mapProfileError(error);
      emit(MemberState.profileFailed(mapped));
    }
  }

  AppException _mapProfileError(AppException error) {
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
