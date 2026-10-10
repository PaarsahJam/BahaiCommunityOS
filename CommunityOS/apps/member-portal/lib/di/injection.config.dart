// GENERATED CODE - DO NOT MODIFY BY HAND
// dart format width=80

// **************************************************************************
// InjectableConfigGenerator
// **************************************************************************

// ignore_for_file: type=lint
// coverage:ignore-file

// ignore_for_file: no_leading_underscores_for_library_prefixes

import 'package:dio/dio.dart' as _i361;
import 'package:flutter_secure_storage/flutter_secure_storage.dart' as _i558;
import 'package:get_it/get_it.dart' as _i174;
import 'package:injectable/injectable.dart' as _i526;
import 'package:member_portal/core/config/app_config.dart' as _i973;
import 'package:member_portal/core/network/auth_interceptor.dart' as _i4;
import 'package:member_portal/core/network/error_mapper.dart' as _i678;
import 'package:member_portal/core/network/refresh_coordinator.dart' as _i245;
import 'package:member_portal/core/storage/token_storage.dart' as _i182;
import 'package:member_portal/di/modules.dart' as _i539;
import 'package:member_portal/features/activities/data/activities_api.dart'
    as _i859;
import 'package:member_portal/features/activities/domain/activities_repository.dart'
    as _i145;
import 'package:member_portal/features/auth/application/auth_bloc.dart'
    as _i952;
import 'package:member_portal/features/auth/data/auth_api.dart' as _i922;
import 'package:member_portal/features/auth/domain/auth_repository.dart'
    as _i552;
import 'package:member_portal/features/meetings/bloc/meetings_bloc.dart'
    as _i950;
import 'package:member_portal/features/meetings/data/meetings_api.dart'
    as _i687;
import 'package:member_portal/features/meetings/data/meetings_repository.dart'
    as _i405;
import 'package:member_portal/features/member/data/member_api.dart' as _i214;
import 'package:member_portal/features/member/domain/member_repository.dart'
    as _i278;
import 'package:member_portal/features/notifications/data/notifications_api.dart'
    as _i697;
import 'package:member_portal/features/notifications/domain/notification_repository.dart'
    as _i142;

extension GetItInjectableX on _i174.GetIt {
// initializes the registration of main-scope dependencies inside of GetIt
  _i174.GetIt init({
    String? environment,
    _i526.EnvironmentFilter? environmentFilter,
  }) {
    final gh = _i526.GetItHelper(
      this,
      environment,
      environmentFilter,
    );
    final appModule = _$AppModule();
    final networkModule = _$NetworkModule();
    final apiModule = _$ApiModule();
    gh.lazySingleton<_i678.ErrorMapper>(() => const _i678.ErrorMapper());
    gh.lazySingleton<_i973.AppConfig>(() => appModule.provideAppConfig());
    gh.lazySingleton<_i558.FlutterSecureStorage>(
        () => appModule.provideSecureStorage());
    gh.singleton<_i361.Dio>(
      () => networkModule.provideBareDio(gh<_i973.AppConfig>()),
      instanceName: 'bareDio',
    );
    gh.lazySingleton<_i182.TokenStorage>(
        () => _i182.SecureTokenStorage(gh<_i558.FlutterSecureStorage>()));
    gh.lazySingleton<_i922.AuthApi>(
        () => apiModule.provideAuthApi(gh<_i361.Dio>(instanceName: 'bareDio')));
    gh.lazySingleton<_i245.RefreshCoordinator>(() => _i245.RefreshCoordinator(
          gh<_i922.AuthApi>(),
          gh<_i182.TokenStorage>(),
        ));
    gh.singleton<_i361.Dio>(() => networkModule.provideAuthorizedDio(
          gh<_i973.AppConfig>(),
          gh<_i182.TokenStorage>(),
          gh<_i245.RefreshCoordinator>(),
        ));
    gh.lazySingleton<_i922.AccountApi>(
        () => apiModule.provideAccountApi(gh<_i361.Dio>()));
    gh.lazySingleton<_i214.MemberApi>(
        () => apiModule.provideMemberApi(gh<_i361.Dio>()));
    gh.lazySingleton<_i697.NotificationApi>(
        () => apiModule.provideNotificationApi(gh<_i361.Dio>()));
    gh.lazySingleton<_i687.MeetingsApi>(
        () => apiModule.provideMeetingsApi(gh<_i361.Dio>()));
    gh.lazySingleton<_i859.ActivitiesApi>(
        () => apiModule.provideActivitiesApi(gh<_i361.Dio>()));
    gh.lazySingleton<_i4.AuthInterceptor>(() => _i4.AuthInterceptor(
          gh<_i182.TokenStorage>(),
          gh<_i245.RefreshCoordinator>(),
        ));
    gh.lazySingleton<_i145.ActivitiesRepository>(
        () => _i145.ActivitiesRepository(
              gh<_i859.ActivitiesApi>(),
              gh<_i678.ErrorMapper>(),
            ));
    gh.lazySingleton<_i405.MeetingsRepository>(() => _i405.MeetingsRepository(
          gh<_i687.MeetingsApi>(),
          gh<_i678.ErrorMapper>(),
          gh<_i245.RefreshCoordinator>(),
        ));
    gh.lazySingleton<_i142.NotificationRepository>(
        () => _i142.NotificationRepository(
              gh<_i697.NotificationApi>(),
              gh<_i678.ErrorMapper>(),
            ));
    gh.lazySingleton<_i278.MemberRepository>(() => _i278.MemberRepository(
          gh<_i922.AccountApi>(),
          gh<_i214.MemberApi>(),
          gh<_i678.ErrorMapper>(),
          gh<_i245.RefreshCoordinator>(),
        ));
    gh.lazySingleton<_i552.AuthRepository>(() => _i552.AuthRepository(
          gh<_i922.AuthApi>(),
          gh<_i922.AccountApi>(),
          gh<_i182.TokenStorage>(),
          gh<_i678.ErrorMapper>(),
        ));
    gh.factory<_i950.MeetingsBloc>(
        () => _i950.MeetingsBloc(gh<_i405.MeetingsRepository>()));
    gh.lazySingleton<_i952.AuthBloc>(() => _i952.AuthBloc(
          gh<_i552.AuthRepository>(),
          gh<_i245.RefreshCoordinator>(),
        ));
    return this;
  }
}

class _$AppModule extends _i539.AppModule {}

class _$NetworkModule extends _i539.NetworkModule {}

class _$ApiModule extends _i539.ApiModule {}
