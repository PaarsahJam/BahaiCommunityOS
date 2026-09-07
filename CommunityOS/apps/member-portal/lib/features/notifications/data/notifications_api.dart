import 'package:dio/dio.dart';
import 'package:retrofit/retrofit.dart';

import 'notifications_dtos.dart';

part 'notifications_api.g.dart';

/// Member-safe notification endpoints served through the authorized client
/// (bearer header + transparent refresh/retry).
///
/// Invariants relied on by the Member Portal:
/// * recipient scope resolves server-side from the caller's identity — no
///   member identifier is ever supplied by the client;
/// * `GET my-notifications/unread-count` is the authoritative unread count;
/// * mark-as-read is a non-idempotent mutation: re-submitting an already-read
///   notification is rejected by the backend, so the repository carries
///   `AuthInterceptor.noAutoRetryKey` and the operation is never transparently
///   refresh+retried.
@RestApi()
abstract class NotificationApi {
  factory NotificationApi(Dio dio, {String baseUrl}) = _NotificationApi;

  @GET('my-notifications')
  Future<List<MemberNotificationSummaryDto>> myNotifications({
    @Query('limit') int limit = 50,
    @Query('offset') int offset = 0,
  });

  @GET('my-notifications/unread-count')
  Future<MemberUnreadCountDto> unreadNotificationCount();

  @POST('my-notifications/{id}/read')
  Future<MemberNotificationSummaryDto> markNotificationRead(
    @Path('id') String id,
    @Extras() Map<String, dynamic>? extra,
  );
}
