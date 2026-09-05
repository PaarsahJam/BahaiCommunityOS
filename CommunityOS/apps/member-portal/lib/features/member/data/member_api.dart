import 'package:dio/dio.dart';
import 'package:retrofit/retrofit.dart';

import 'member_dtos.dart';

part 'member_api.g.dart';

/// Community member-facing endpoints, all served through the authorized
/// client (bearer header + transparent refresh/retry).
///
/// Invariants relied on by the Member Portal:
/// * `my-person` is resolved server-side from the caller's identity — no
///   person identifier is ever supplied by the client.
/// * `memberships/by-person/{personId}` returns 204/200-null when the person
///   has no membership record.
@RestApi()
abstract class MemberApi {
  factory MemberApi(Dio dio, {String baseUrl}) = _MemberApi;

  @GET('my-person')
  Future<PersonDto> getMyPerson();

  @GET('persons/{id}')
  Future<PersonDetailDto> getPerson(@Path('id') String id);

  @GET('memberships/by-person/{personId}')
  Future<MembershipDto?> getMembershipByPerson(
      @Path('personId') String personId);
}
