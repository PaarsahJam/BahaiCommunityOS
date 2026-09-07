import 'package:flutter/material.dart';
import 'package:flutter_bloc/flutter_bloc.dart';
import 'package:intl/intl.dart';
import 'package:member_portal/l10n/generated/app_localizations.dart';

import '../../../core/error/app_exception.dart';
import '../../../core/ui/exception_message.dart';
import '../../../di/injection.dart';
import '../../auth/application/auth_bloc.dart';
import '../../auth/application/auth_event.dart';
import '../../auth/data/auth_dtos.dart';
import '../../member/domain/member_repository.dart';
import '../application/account_bloc.dart';
import '../application/account_event.dart';
import '../application/account_state.dart';
import '../domain/account_exceptions.dart';

/// Personal account and security page, rendered inside the authenticated shell.
///
/// One [AccountBloc] per page instance (page-scoped): the overview comes from
/// the authoritative `/me`, security activity is a read-only recent list from
/// `/me/security-events`, and password change is a guarded non-idempotent
/// mutation. After a successful change the whole token family is revoked
/// server-side, so this page deliberately hands control back to the existing
/// centralized session-expiry/re-authentication path (`AuthEvent.sessionExpired`).
class AccountPage extends StatefulWidget {
  const AccountPage({super.key, this.createAccountBloc});

  /// Injectable factory for tests; defaults to a bloc backed by the DI
  /// [MemberRepository].
  final AccountBloc Function()? createAccountBloc;

  @override
  State<AccountPage> createState() => _AccountPageState();
}

class _AccountPageState extends State<AccountPage> {
  late final AccountBloc _bloc = (widget.createAccountBloc ??
      () => AccountBloc(getIt<MemberRepository>()))();

  final _formKey = GlobalKey<FormState>();
  final _currentController = TextEditingController();
  final _newController = TextEditingController();
  final _confirmController = TextEditingController();

  @override
  void initState() {
    super.initState();
    WidgetsBinding.instance.addPostFrameCallback((_) {
      if (mounted) {
        _bloc.add(const AccountEvent.requested());
      }
    });
  }

  @override
  void dispose() {
    _currentController.dispose();
    _newController.dispose();
    _confirmController.dispose();
    _bloc.close();
    super.dispose();
  }

  void _retry() => _bloc.add(const AccountEvent.retryRequested());

  void _retryEvents() =>
      _bloc.add(const AccountEvent.securityEventsRequested());

  void _submitPassword() {
    final state = _bloc.state;
    if (state is! AccountLoaded) return;
    if (state.passwordStatus is PasswordSubmitting) return;
    if (!(_formKey.currentState?.validate() ?? false)) return;

    _bloc.add(AccountEvent.passwordChangeRequested(
      currentPassword: _currentController.text,
      newPassword: _newController.text,
    ));
  }

  void _clearPasswordFields() {
    _currentController.clear();
    _newController.clear();
    _confirmController.clear();
  }

  @override
  Widget build(BuildContext context) {
    final l10n = AppLocalizations.of(context)!;
    return BlocProvider<AccountBloc>.value(
      value: _bloc,
      child: BlocListener<AccountBloc, AccountState>(
        listener: (context, state) {
          if (state is! AccountLoaded) return;
          if (state.passwordStatus is PasswordSucceeded) {
            // The backend has revoked every session for this account. Never
            // reuse the now-invalid token family: deliberately run the
            // existing centralized re-authentication path.
            _clearPasswordFields();
            context.read<AuthBloc>().add(const AuthEvent.sessionExpired());
          }
        },
        child: Scaffold(
          appBar: AppBar(title: Text(l10n.accountTitle)),
          body: BlocBuilder<AccountBloc, AccountState>(
            builder: (context, state) {
              return switch (state) {
                AccountLoaded() => _AccountContent(
                    account: state.account,
                    securityEvents: state.securityEvents,
                    securityEventsLoading: state.isSecurityEventsLoading,
                    securityEventsError: state.securityEventsError,
                    passwordStatus: state.passwordStatus,
                    formKey: _formKey,
                    currentController: _currentController,
                    newController: _newController,
                    confirmController: _confirmController,
                    onSubmitPassword: _submitPassword,
                    onRetryEvents: _retryEvents,
                  ),
                AccountFailed(:final error) => _AccountErrorView(
                    error: error,
                    onRetry: _retry,
                  ),
                _ => const Center(child: CircularProgressIndicator()),
              };
            },
          ),
        ),
      ),
    );
  }
}

class _AccountContent extends StatelessWidget {
  const _AccountContent({
    required this.account,
    required this.securityEvents,
    required this.securityEventsLoading,
    required this.securityEventsError,
    required this.passwordStatus,
    required this.formKey,
    required this.currentController,
    required this.newController,
    required this.confirmController,
    required this.onSubmitPassword,
    required this.onRetryEvents,
  });

  final UserAccountDto account;
  final List<SecurityEventDto> securityEvents;
  final bool securityEventsLoading;
  final AppException? securityEventsError;
  final PasswordStatus passwordStatus;
  final GlobalKey<FormState> formKey;
  final TextEditingController currentController;
  final TextEditingController newController;
  final TextEditingController confirmController;
  final VoidCallback onSubmitPassword;
  final VoidCallback onRetryEvents;

  @override
  Widget build(BuildContext context) {
    return ListView(
      padding: const EdgeInsets.all(16),
      children: [
        _OverviewCard(account: account),
        const SizedBox(height: 16),
        _PasswordCard(
          formKey: formKey,
          currentController: currentController,
          newController: newController,
          confirmController: confirmController,
          passwordStatus: passwordStatus,
          onSubmit: onSubmitPassword,
        ),
        const SizedBox(height: 16),
        _SecurityActivityCard(
          securityEvents: securityEvents,
          loading: securityEventsLoading,
          error: securityEventsError,
          onRetry: onRetryEvents,
        ),
      ],
    );
  }
}

class _OverviewCard extends StatelessWidget {
  const _OverviewCard({required this.account});

  final UserAccountDto account;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final l10n = AppLocalizations.of(context)!;
    final mfaEnabled =
        account.mfaMethods.any((m) => m.isVerified && m.isActive);
    final dateFormat = DateFormat.yMMMd(l10n.localeName);

    return Card(
      child: Padding(
        padding: const EdgeInsets.all(16),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Row(
              children: [
                Icon(Icons.manage_accounts_outlined,
                    color: theme.colorScheme.primary),
                const SizedBox(width: 8),
                Text(l10n.accountTitle, style: theme.textTheme.titleMedium),
              ],
            ),
            const SizedBox(height: 12),
            _infoRow(l10n.accountEmail, account.email),
            _infoRow(
              l10n.accountStatus,
              accountStatusLabel(context, account.status),
            ),
            _infoRow(
              l10n.accountMemberSince,
              dateFormat.format(account.createdOn),
            ),
            const SizedBox(height: 8),
            Chip(
              label: Text(
                mfaEnabled ? l10n.accountMfaEnabled : l10n.accountMfaDisabled,
                style: theme.textTheme.labelMedium,
              ),
              avatar: Icon(
                mfaEnabled ? Icons.verified_user : Icons.shield_outlined,
                size: 18,
                color: mfaEnabled
                    ? theme.colorScheme.primary
                    : theme.colorScheme.outline,
              ),
            ),
          ],
        ),
      ),
    );
  }

  Widget _infoRow(String label, String value) {
    return Padding(
      padding: const EdgeInsets.only(bottom: 8),
      child: Row(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          SizedBox(
            width: 120,
            child: Text(
              label,
              style: const TextStyle(fontWeight: FontWeight.w600),
            ),
          ),
          Expanded(child: Text(value)),
        ],
      ),
    );
  }
}

class _PasswordCard extends StatelessWidget {
  const _PasswordCard({
    required this.formKey,
    required this.currentController,
    required this.newController,
    required this.confirmController,
    required this.passwordStatus,
    required this.onSubmit,
  });

  final GlobalKey<FormState> formKey;
  final TextEditingController currentController;
  final TextEditingController newController;
  final TextEditingController confirmController;
  final PasswordStatus passwordStatus;
  final VoidCallback onSubmit;

  String _serverFieldError(String property) {
    final status = passwordStatus;
    if (status is! PasswordFailed) return '';
    final error = status.error;
    if (error is! PasswordValidationException) return '';
    return error.fieldErrors[property] ?? '';
  }

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final l10n = AppLocalizations.of(context)!;
    final submitting = passwordStatus is PasswordSubmitting;
    final errorText = switch (passwordStatus) {
      PasswordFailed(:final error) => exceptionMessage(context, error),
      _ => null,
    };

    return Card(
      child: Padding(
        padding: const EdgeInsets.all(16),
        child: Form(
          key: formKey,
          child: Column(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Text(l10n.accountPasswordSectionTitle,
                  style: theme.textTheme.titleMedium),
              const SizedBox(height: 4),
              Text(l10n.accountPasswordSectionBody,
                  style: theme.textTheme.bodySmall),
              const SizedBox(height: 12),
              TextFormField(
                key: const Key('current-password'),
                controller: currentController,
                obscureText: true,
                autocorrect: false,
                enableSuggestions: false,
                decoration: InputDecoration(
                  labelText: l10n.accountCurrentPassword,
                  errorText: _serverFieldError('currentpassword'),
                ),
                validator: (value) => (value == null || value.isEmpty)
                    ? l10n.accountCurrentPasswordRequired
                    : null,
              ),
              const SizedBox(height: 12),
              TextFormField(
                key: const Key('new-password'),
                controller: newController,
                obscureText: true,
                autocorrect: false,
                enableSuggestions: false,
                decoration: InputDecoration(
                  labelText: l10n.accountNewPassword,
                  errorText: _serverFieldError('newpassword').isEmpty
                      ? null
                      : _serverFieldError('newpassword'),
                ),
                validator: (value) => (value == null || value.isEmpty)
                    ? l10n.accountNewPasswordRequired
                    : null,
              ),
              const SizedBox(height: 12),
              TextFormField(
                key: const Key('confirm-password'),
                controller: confirmController,
                obscureText: true,
                autocorrect: false,
                enableSuggestions: false,
                decoration: InputDecoration(
                  labelText: l10n.accountConfirmPassword,
                  errorText: _serverFieldError('confirmpassword').isEmpty
                      ? null
                      : _serverFieldError('confirmpassword'),
                ),
                validator: (value) {
                  if (value == null || value.isEmpty) {
                    return l10n.accountConfirmPasswordRequired;
                  }
                  if (value != newController.text) {
                    return l10n.accountConfirmPasswordMismatch;
                  }
                  return null;
                },
              ),
              if (errorText != null) ...[
                const SizedBox(height: 8),
                Text(
                  errorText,
                  style: TextStyle(color: theme.colorScheme.error),
                ),
              ],
              const SizedBox(height: 16),
              SizedBox(
                width: double.infinity,
                child: FilledButton(
                  onPressed: submitting ? null : onSubmit,
                  child: submitting
                      ? const SizedBox(
                          width: 20,
                          height: 20,
                          child: CircularProgressIndicator(strokeWidth: 2),
                        )
                      : Text(l10n.accountChangePassword),
                ),
              ),
            ],
          ),
        ),
      ),
    );
  }
}

class _SecurityActivityCard extends StatelessWidget {
  const _SecurityActivityCard({
    required this.securityEvents,
    required this.loading,
    required this.error,
    required this.onRetry,
  });

  final List<SecurityEventDto> securityEvents;
  final bool loading;
  final AppException? error;
  final VoidCallback onRetry;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final l10n = AppLocalizations.of(context)!;

    return Card(
      child: Padding(
        padding: const EdgeInsets.all(16),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Text(l10n.accountSecurityActivity,
                style: theme.textTheme.titleMedium),
            const SizedBox(height: 8),
            if (loading && securityEvents.isEmpty)
              const Padding(
                padding: EdgeInsets.symmetric(vertical: 12),
                child: Center(
                  child: SizedBox(
                    width: 20,
                    height: 20,
                    child: CircularProgressIndicator(strokeWidth: 2),
                  ),
                ),
              )
            else if (error != null && securityEvents.isEmpty)
              _SecurityErrorView(error: error!, onRetry: onRetry)
            else if (securityEvents.isEmpty)
              Padding(
                padding: const EdgeInsets.symmetric(vertical: 12),
                child: Text(
                  l10n.accountSecurityEventsEmpty,
                  style: theme.textTheme.bodyMedium,
                ),
              )
            else
              for (final event in _newestFirst(securityEvents))
                _SecurityEventTile(event: event),
          ],
        ),
      ),
    );
  }

  List<SecurityEventDto> _newestFirst(List<SecurityEventDto> events) {
    final sorted = [...events];
    sorted.sort((a, b) => b.occurredOn.compareTo(a.occurredOn));
    return sorted;
  }
}

class _SecurityEventTile extends StatelessWidget {
  const _SecurityEventTile({required this.event});

  final SecurityEventDto event;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final l10n = AppLocalizations.of(context)!;
    final timestamp =
        DateFormat.yMMMd(l10n.localeName).add_jm().format(event.occurredOn);

    return ListTile(
      contentPadding: EdgeInsets.zero,
      leading: Icon(Icons.schedule, color: theme.colorScheme.outline),
      title: Text(accountSecurityEventLabel(context, event.eventType)),
      subtitle: Text(timestamp),
    );
  }
}

class _SecurityErrorView extends StatelessWidget {
  const _SecurityErrorView({required this.error, required this.onRetry});

  final AppException error;
  final VoidCallback onRetry;

  @override
  Widget build(BuildContext context) {
    final l10n = AppLocalizations.of(context)!;
    return Row(
      children: [
        Expanded(child: Text(exceptionMessage(context, error))),
        TextButton.icon(
          onPressed: onRetry,
          icon: const Icon(Icons.refresh),
          label: Text(l10n.homeRetry),
        ),
      ],
    );
  }
}

class _AccountErrorView extends StatelessWidget {
  const _AccountErrorView({required this.error, this.onRetry});

  final AppException error;
  final VoidCallback? onRetry;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final l10n = AppLocalizations.of(context)!;
    return Center(
      child: Padding(
        padding: const EdgeInsets.all(24),
        child: Column(
          mainAxisSize: MainAxisSize.min,
          children: [
            Icon(Icons.error_outline, size: 48, color: theme.colorScheme.error),
            const SizedBox(height: 12),
            Text(exceptionMessage(context, error), textAlign: TextAlign.center),
            if (onRetry != null) ...[
              const SizedBox(height: 16),
              OutlinedButton.icon(
                onPressed: onRetry,
                icon: const Icon(Icons.refresh),
                label: Text(l10n.homeRetry),
              ),
            ],
          ],
        ),
      ),
    );
  }
}

/// Localized label for the account status string returned by the Identity
/// service. Matching is case-insensitive: the service emits PascalCase
/// statuses without a casing contract.
String accountStatusLabel(BuildContext context, String status) {
  final l10n = AppLocalizations.of(context);
  if (l10n == null) return status;
  return switch (status.toLowerCase()) {
    'active' => l10n.accountStatusActive,
    'pending' || 'pendingverification' => l10n.accountStatusPending,
    'locked' => l10n.accountStatusLocked,
    'deactivated' => l10n.accountStatusDeactivated,
    _ => l10n.accountStatusUnknown,
  };
}

/// Stable localized label for a security event, keyed by the backend's
/// `EventType` contract. The free-text `Description` is never treated as an
/// authoritative event classification.
String accountSecurityEventLabel(BuildContext context, String eventType) {
  final l10n = AppLocalizations.of(context);
  if (l10n == null) return eventType;
  final tail = eventType.split('.').last.toLowerCase();
  return switch (tail) {
    'succeeded' || 'success' => l10n.accountEventLoginSucceeded,
    'failed' || 'failure' => l10n.accountEventLoginFailed,
    'required' => l10n.accountEventMfaRequired,
    'changed' => l10n.accountEventPasswordChanged,
    'reusedetected' => l10n.accountEventRefreshTokenReuse,
    'codeissued' => l10n.accountEventAuthorizeCodeIssued,
    'issued' => l10n.accountEventTokenIssued,
    _ => l10n.accountEventUnknown,
  };
}
