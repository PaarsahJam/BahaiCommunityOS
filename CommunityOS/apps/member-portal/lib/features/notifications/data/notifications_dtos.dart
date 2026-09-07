import 'package:freezed_annotation/freezed_annotation.dart';

part 'notifications_dtos.freezed.dart';
part 'notifications_dtos.g.dart';

/// Member-safe notification summary from `GET /my-notifications`.
///
/// Mirrors the committed member read contract exactly: notification identity,
/// type, channel, lifecycle status, creation time, member-visible content and
/// recipient-specific read state. The contract carries no recipient
/// distribution, no source/scope metadata and no organization identifiers.
@freezed
abstract class MemberNotificationSummaryDto
    with _$MemberNotificationSummaryDto {
  const factory MemberNotificationSummaryDto({
    required String id,
    required String typeCode,
    required String channel,
    required String status,
    required String title,
    required String body,
    required bool isRead,
    DateTime? readAt,
    required DateTime createdOn,
  }) = _MemberNotificationSummaryDto;

  factory MemberNotificationSummaryDto.fromJson(Map<String, dynamic> json) =>
      _$MemberNotificationSummaryDtoFromJson(json);
}

/// Recipient-scoped unread count from `GET /my-notifications/unread-count`.
///
/// The server's value is authoritative. It is never derived from a loaded page
/// and a failed fetch is never reported as zero.
@freezed
abstract class MemberUnreadCountDto with _$MemberUnreadCountDto {
  const factory MemberUnreadCountDto({required int count}) =
      _MemberUnreadCountDto;

  factory MemberUnreadCountDto.fromJson(Map<String, dynamic> json) =>
      _$MemberUnreadCountDtoFromJson(json);
}
