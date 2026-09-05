import 'package:flutter/material.dart';
import 'package:sentry_flutter/sentry_flutter.dart';

import 'app/app.dart';
import 'core/config/app_config.dart';
import 'di/injection.dart';
import 'features/auth/application/auth_bloc.dart';
import 'features/auth/application/auth_event.dart';

Future<void> main() async {
  WidgetsFlutterBinding.ensureInitialized();

  await configureDependencies();

  final config = getIt<AppConfig>();
  final authBloc = getIt<AuthBloc>();
  authBloc.add(const AuthEvent.appStarted());

  const app = MemberPortalApp();
  if (config.sentryDsn.isNotEmpty) {
    await SentryFlutter.init(
      (options) {
        options.dsn = config.sentryDsn;
        options.tracesSampleRate = 0.1;
      },
      appRunner: () => runApp(app),
    );
    return;
  }
  runApp(app);
}
