import 'package:dio/dio.dart';
import 'package:injectable/injectable.dart';

import '../../../core/error/app_exception.dart';
import '../../../core/network/error_mapper.dart';
import '../data/member_api.dart';
import '../data/member_dtos.dart';
import 'member_models.dart';

@LazySingleton()
class MemberRepository {
  MemberRepository(this._api, this._mapper);

  final MemberApi _api;
  final ErrorMapper _mapper;

  Future<MemberHomeResult> loadMemberHome() async {
    final PersonDto person;
    try {
      person = await _guard(() => _api.getMyPerson());
    } on AppException catch (error) {
      return _mapHomeError(error);
    }

    MembershipDto? membership;
    try {
      membership = await _guard(() => _api.getMembershipByPerson(person.id));
    } on AppException {
      // A membership-read failure must not hide the person's profile; the
      // home screen simply shows "no membership record".
      membership = null;
    }

    return MemberHomeResult.resolved(person: person, membership: membership);
  }

  MemberHomeResult _mapHomeError(AppException error) {
    if (error is NotFoundException) {
      return const MemberHomeResult.unlinkedAccount();
    }
    if (error is UnauthorizedException) {
      return const MemberHomeResult.unauthenticated();
    }
    if (error is ForbiddenException) {
      return MemberHomeResult.forbidden(
        ForbiddenException(
          error.message,
          messageKey: 'profile_forbidden',
          code: error.code,
          statusCode: error.statusCode,
        ),
      );
    }
    return MemberHomeResult.failed(error);
  }

  Future<PersonDetailDto> personDetail(String personId) =>
      _guard(() => _api.getPerson(personId));

  Future<T> _guard<T>(Future<T> Function() action) async {
    try {
      return await action();
    } on DioException catch (error) {
      throw _mapper.map(error);
    }
  }
}
