/// Runtime configuration for the Member Portal.
///
/// Values are supplied at build/run time via `--dart-define`. The default API
/// base URL targets the local CommunityOS API Gateway during development and
/// is intentionally not a production infrastructure value.
class AppConfig {
  const AppConfig({
    required this.apiBaseUrl,
    required this.sentryDsn,
    required this.logRequests,
  });

  factory AppConfig.fromEnvironment() => const AppConfig(
        apiBaseUrl: String.fromEnvironment(
          'API_BASE_URL',
          defaultValue: 'http://localhost:5000/api/v1',
        ),
        sentryDsn: String.fromEnvironment('SENTRY_DSN', defaultValue: ''),
        logRequests: bool.fromEnvironment('LOG_REQUESTS', defaultValue: false),
      );

  final String apiBaseUrl;
  final String sentryDsn;
  final bool logRequests;
}
