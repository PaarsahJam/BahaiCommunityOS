import 'package:bloc/bloc.dart';

import '../../../core/error/app_exception.dart';
import '../domain/notification_repository.dart';
import 'home_unread_event.dart';
import 'home_unread_state.dart';

/// Loads the authoritative unread count for one Home notification-badge
/// instance.
///
/// Small and Home-scoped by design (never global): it exists so Home never
/// relies on a page-derived count or on global notification state. The badge
/// instance refreshes it when it re-gains focus after returning from the
/// Notification Center.
class HomeUnreadBloc extends Bloc<HomeUnreadEvent, HomeUnreadState> {
  HomeUnreadBloc(this._repository) : super(const HomeUnreadState.initial()) {
    on<HomeUnreadRefresh>(_onRefresh);
  }

  final NotificationRepository _repository;

  Future<void> _onRefresh(
    HomeUnreadRefresh event,
    Emitter<HomeUnreadState> emit,
  ) async {
    if (state is HomeUnreadLoading) return;
    emit(const HomeUnreadState.loading());
    try {
      final count = await _repository.unreadCount();
      // The widget may have been disposed while loading; a closed bloc must
      // never emit.
      if (isClosed) return;
      emit(HomeUnreadState.loaded(count));
    } on AppException catch (error) {
      if (isClosed) return;
      // A failed count is never converted into zero.
      emit(HomeUnreadState.failed(error));
    }
  }
}
