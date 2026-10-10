import 'package:dio/dio.dart';
import 'package:retrofit/retrofit.dart';

import 'activities_dtos.dart';

part 'activities_api.g.dart';

/// Community activity-facing endpoints, all served through the authorized
/// client (bearer header + transparent refresh/retry).
///
/// `GET /activities` returns a bare JSON array (the backend serializes
/// `IReadOnlyList<ActivityDto>` directly), so the list method is typed as
/// `List<ActivityDto>` — there is no wrapper object.
@RestApi()
abstract class ActivitiesApi {
  factory ActivitiesApi(Dio dio, {String baseUrl}) = _ActivitiesApi;

  @GET('activities')
  Future<List<ActivityDto>> listActivities({
    @Query('from') DateTime? from,
    @Query('to') DateTime? to,
    @Query('organizationUnitId') String? organizationUnitId,
  });

  @GET('activities/{id}')
  Future<ActivityDto> detailActivity({
    @Path('id') required String id,
  });
}
