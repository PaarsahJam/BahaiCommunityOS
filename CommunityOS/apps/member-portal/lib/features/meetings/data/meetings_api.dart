import 'package:dio/dio.dart';
import 'package:retrofit/retrofit.dart';

import 'meetings_dtos.dart';

part 'meetings_api.g.dart';

/// Community meeting-facing endpoints, all served through the authorized
/// client (bearer header + transparent refresh/retry).
///
/// Invariants relied on by the Member Portal:
/// * `my-person` is resolved server-side from the caller's identity — no
///   person identifier is ever supplied by the client.
/// * `memberships/by-person/{personId}` returns 204/200-null when the person
///   has no membership record.
/// * `GET /meetings` returns a bare JSON array (the backend serializes
///   `IReadOnlyList<MeetingDto>` directly), so the list method is typed as
///   `List<MeetingDto>` — there is no wrapper object.
@RestApi()
abstract class MeetingsApi {
  factory MeetingsApi(Dio dio, {String baseUrl}) = _MeetingsApi;

  @GET('meetings')
  Future<List<MeetingDto>> listMeetings({
    @Query('from') DateTime? from,
    @Query('to') DateTime? to,
    @Query('organizationUnitId') String? organizationUnitId,
  });

  @GET('meetings/{id}')
  Future<MeetingDto> getMeeting({
    @Path('id') required String id,
  });
}