import 'package:flutter/material.dart';
import 'package:member_portal/l10n/generated/app_localizations.dart';

class SplashPage extends StatelessWidget {
  const SplashPage({super.key});

  @override
  Widget build(BuildContext context) {
    final l10n = AppLocalizations.of(context);
    return Scaffold(
      body: Center(
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: [
            const CircularProgressIndicator(),
            const SizedBox(height: 16),
            Text(l10n?.splashLoading ?? 'Loading…'),
          ],
        ),
      ),
    );
  }
}
