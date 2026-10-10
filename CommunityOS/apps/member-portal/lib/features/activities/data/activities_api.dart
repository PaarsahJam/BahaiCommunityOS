import 'package:dio/dio.dart';
import 'package:retrofit/retrofit.dart';

import 'activities_dtos.dart';

part 'activities_api.g.dart';

/// Community activity-facing endpoints, all served through the authorized
/// client (bearer header + transparent refresh/retry).
@RestApi()
abstract class ActivitiesApi {
  factory ActivitiesApi(Dio dio, {String baseUrl}) = _ActivitiesApi;

  @GET('activities')
  Future<ActivityListDto> listActivities({
    @Query('from') DateTime? from,
    @Query('to') DateTime? to,
    @Query('organizationUnitId') String? organizationUnitId,
  });

  @GET('activities/{id}')
  Future<ActivityDto> detailActivity({
    @Path('id') String id,
  });
}