import 'package:flutter/material.dart';
import 'package:flutter_bloc/flutter_bloc.dart';
import 'package:member_portal/l10n/generated/app_localizations.dart';

import '../core/router/app_router.dart';
import '../di/injection.dart';
import '../features/auth/application/auth_bloc.dart';

class MemberPortalApp extends StatelessWidget {
  const MemberPortalApp({super.key});

  @override
  Widget build(BuildContext context) {
    final authBloc = getIt<AuthBloc>();

    return BlocProvider<AuthBloc>(
      create: (_) => authBloc,
      child: MaterialApp.router(
        title: 'CommunityOS Member Portal',
        theme: ThemeData(
          useMaterial3: true,
          colorSchemeSeed: const Color(0xFF00668C),
        ),
        routerConfig: AppRouter.build(authBloc),
        localizationsDelegates: AppLocalizations.localizationsDelegates,
        supportedLocales: AppLocalizations.supportedLocales,
      ),
    );
  }
}
