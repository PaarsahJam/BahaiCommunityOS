import 'package:dio/dio.dart';
import 'package:flutter_secure_storage/flutter_secure_storage.dart';
import 'package:injectable/injectable.dart';

import '../core/config/app_config.dart';
import '../core/network/dio_factory.dart';
import '../core/network/refresh_coordinator.dart';
import '../core/storage/token_storage.dart';
import '../features/auth/data/auth_api.dart';
import '../features/member/data/member_api.dart';
import '../features/meetings/data/meetings_api.dart';
import '../features/activities/data/activities_api.dart';
import '../features/notifications/data/notifications_api.dart';

@module
abstract class AppModule {
  @LazySingleton()
  AppConfig provideAppConfig() => AppConfig.fromEnvironment();

  @LazySingleton()
  FlutterSecureStorage provideSecureStorage() => const FlutterSecureStorage();
}

@module
abstract class NetworkModule {
  /// Client with no interceptors; used only for `auth/login` and
  /// `auth/refresh` so refresh can never recurse through the auth interceptor.
  @Named('bareDio')
  @singleton
  Dio provideBareDio(AppConfig config) => const DioFactory()
      .buildBare(config.apiBaseUrl, logRequests: config.logRequests);

  /// Client with the auth interceptor; all other API traffic goes through it.
  @singleton
  Dio provideAuthorizedDio(
    AppConfig config,
    TokenStorage tokenStorage,
    RefreshCoordinator coordinator,
  ) =>
      const DioFactory().buildAuthorized(
        config.apiBaseUrl,
        tokenStorage: tokenStorage,
        coordinator: coordinator,
        logRequests: config.logRequests,
      );
}

@module
abstract class ApiModule {
  @LazySingleton()
  AuthApi provideAuthApi(@Named('bareDio') Dio dio) => AuthApi(dio);

  @LazySingleton()
  AccountApi provideAccountApi(Dio dio) => AccountApi(dio);

  @LazySingleton()
  MemberApi provideMemberApi(Dio dio) => MemberApi(dio);

  @LazySingleton()
  NotificationApi provideNotificationApi(Dio dio) => NotificationApi(dio);

  @LazySingleton()
  MeetingsApi provideMeetingsApi(Dio dio) => MeetingsApi(dio);

  @LazySingleton()
  ActivitiesApi provideActivitiesApi(Dio dio) => ActivitiesApi(dio);
}
