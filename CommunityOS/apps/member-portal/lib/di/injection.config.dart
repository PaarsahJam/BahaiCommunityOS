// dart format width=80
// GENERATED CODE - DO NOT MODIFY BY HAND

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
import 'package:member_portal/features/auth/application/auth_bloc.dart'
    as _i952;
import 'package:member_portal/features/auth/data/auth_api.dart' as _i922;
import 'package:member_portal/features/auth/domain/auth_repository.dart'
    as _i552;
import 'package:member_portal/features/member/application/member_bloc.dart'
    as _i637;
import 'package:member_portal/features/member/data/member_api.dart' as _i214;
import 'package:member_portal/features/member/domain/member_repository.dart'
    as _i278;

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
    gh.lazySingleton<_i4.AuthInterceptor>(() => _i4.AuthInterceptor(
          gh<_i182.TokenStorage>(),
          gh<_i245.RefreshCoordinator>(),
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
    gh.lazySingleton<_i278.MemberRepository>(() => _i278.MemberRepository(
          gh<_i214.MemberApi>(),
          gh<_i678.ErrorMapper>(),
        ));
    gh.lazySingleton<_i552.AuthRepository>(() => _i552.AuthRepository(
          gh<_i922.AuthApi>(),
          gh<_i922.AccountApi>(),
          gh<_i182.TokenStorage>(),
          gh<_i678.ErrorMapper>(),
        ));
    gh.lazySingleton<_i952.AuthBloc>(() => _i952.AuthBloc(
          gh<_i552.AuthRepository>(),
          gh<_i245.RefreshCoordinator>(),
        ));
    gh.lazySingleton<_i637.MemberBloc>(
        () => _i637.MemberBloc(gh<_i278.MemberRepository>()));
    return this;
  }
}

class _$AppModule extends _i539.AppModule {}

class _$NetworkModule extends _i539.NetworkModule {}

class _$ApiModule extends _i539.ApiModule {}
