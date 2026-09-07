import 'package:dio/dio.dart';
import 'package:injectable/injectable.dart';

import '../../../core/network/auth_interceptor.dart';
import '../../../core/network/error_mapper.dart';
import '../data/notifications_api.dart';
import '../data/notifications_dtos.dart';

/// Member-safe notifications repository.
///
/// The backend remains authoritative for scope, content, read state and the
/// unread count; this client only maps outcomes into typed results and never
/// re-authorizes or derives state locally. The unread count is always consumed
/// from the dedicated endpoint.
@LazySingleton()
class NotificationRepository {
  NotificationRepository(this._api, this._mapper);

  final NotificationApi _api;
  final ErrorMapper _mapper;

  /// Loads one page of the authenticated member's notifications, newest-first,
  /// using the backend's bounded pagination. A page shorter than [limit]
  /// indicates the terminal page (the contract exposes no total/hasMore field,
  /// so page-size comparison is the conservative end-of-list signal).
  Future<List<MemberNotificationSummaryDto>> myNotifications({
    int limit = 50,
    int offset = 0,
  }) =>
      _guard(() => _api.myNotifications(limit: limit, offset: offset));

  /// The authoritative recipient-scoped unread count.
  Future<int> unreadCount() async {
    final dto = await _guard(() => _api.unreadNotificationCount());
    return dto.count;
  }

  /// Marks the authenticated recipient's notification [id] read and returns the
  /// backend's authoritative updated [MemberNotificationSummaryDto] (including
  /// `readAt`).
  ///
  /// Non-idempotent mutation: it carries [AuthInterceptor.noAutoRetryKey] so
  /// the transparent 401 refresh+retry can never re-submit it, matching the
  /// project convention for every authenticated mutation. Local read state is
  /// only ever updated from a successful response.
  Future<MemberNotificationSummaryDto> markRead(String id) =>
      _guard(() => _api.markNotificationRead(id, {
            AuthInterceptor.noAutoRetryKey: true,
          }));

  Future<T> _guard<T>(Future<T> Function() action) async {
    try {
      return await action();
    } on DioException catch (error) {
      throw _mapper.map(error);
    }
  }
}
