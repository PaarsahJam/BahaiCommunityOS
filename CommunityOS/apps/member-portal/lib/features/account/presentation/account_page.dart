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
import '../application/security_bloc.dart';
import '../application/security_event.dart';
import '../application/security_state.dart';
import '../domain/account_exceptions.dart';

/// Personal account and security page, rendered inside the authenticated shell.
///
/// One [AccountBloc] per page instance owns the overview (/me), read-only
/// security activity, and the guarded password mutation; one page-scoped
/// [SecurityBloc] owns the read-only session list and the TOTP enrollment
/// flow. After a successful password change the whole token family is revoked
/// server-side, so this page deliberately hands control back to the existing
/// centralized session-expiry/re-authentication path
/// (`AuthEvent.sessionExpired`). A successful MFA enrollment never marks the
/// account enabled: the page asks the account overview to refresh `/me`.
class AccountPage extends StatefulWidget {
  const AccountPage({
    super.key,
    this.createAccountBloc,
    this.createSecurityBloc,
  });

  /// Injectable factory for the account bloc; defaults to a bloc backed by the
  /// DI [MemberRepository].
  final AccountBloc Function()? createAccountBloc;

  /// Injectable factory for the page-scoped security bloc; defaults to a bloc
  /// backed by the DI [MemberRepository].
  final SecurityBloc Function()? createSecurityBloc;

  @override
  State<AccountPage> createState() => _AccountPageState();
}

class _AccountPageState extends State<AccountPage> {
  late final AccountBloc _bloc = (widget.createAccountBloc ??
      () => AccountBloc(getIt<MemberRepository>()))();
  late final SecurityBloc _securityBloc = (widget.createSecurityBloc ??
      () => SecurityBloc(getIt<MemberRepository>()))();

  final _formKey = GlobalKey<FormState>();
  final _currentController = TextEditingController();
  final _newController = TextEditingController();
  final _confirmController = TextEditingController();
  final _mfaFormKey = GlobalKey<FormState>();
  final _mfaCodeController = TextEditingController();

  @override
  void initState() {
    super.initState();
    WidgetsBinding.instance.addPostFrameCallback((_) {
      if (mounted) {
        _bloc.add(const AccountEvent.requested());
        _securityBloc.add(const SecurityEvent.sessionsRequested());
      }
    });
  }

  @override
  void dispose() {
    _currentController.dispose();
    _newController.dispose();
    _confirmController.dispose();
    _mfaCodeController.dispose();
    _securityBloc.close();
    _bloc.close();
    super.dispose();
  }

  void _retry() => _bloc.add(const AccountEvent.retryRequested());

  void _retryEvents() =>
      _bloc.add(const AccountEvent.securityEventsRequested());

  void _retrySessions() =>
      _securityBloc.add(const SecurityEvent.sessionsRequested());

  /// Retries a failed revocation directly. The session is explicit and was
  /// already confirmed by the user before the first attempt; nothing was
  /// revoked, so re-confirming would be pointless friction.
  void _retryRevokeSession(String sessionId) =>
      _securityBloc.add(SecurityEvent.sessionRevokeRequested(sessionId));

  /// Retries a failed revoke-others operation directly. Nothing was revoked,
  /// so re-confirming would be pointless friction.
  void _retryRevokeOthers() =>
      _securityBloc.add(const SecurityEvent.revokeOthersRequested());

  Future<void> _confirmRevokeOthers(BuildContext context) async {
    final l10n = AppLocalizations.of(context)!;
    final confirmed = await showDialog<bool>(
      context: context,
      builder: (dialogContext) => AlertDialog(
        title: Text(l10n.accountSessionRevokeOthersTitle),
        content: Text(l10n.accountSessionRevokeOthersBody),
        actions: [
          TextButton(
            onPressed: () => Navigator.of(dialogContext).pop(false),
            child: Text(l10n.accountSessionRevokeOthersCancel),
          ),
          FilledButton(
            key: const Key('session-revoke-others-confirm'),
            onPressed: () => Navigator.of(dialogContext).pop(true),
            child: Text(l10n.accountSessionRevokeOthersConfirm),
          ),
        ],
      ),
    );
    if (confirmed == true && mounted) {
      _securityBloc.add(const SecurityEvent.revokeOthersRequested());
    }
  }

  Future<void> _confirmRevokeSession(
    BuildContext context,
    String sessionId,
  ) async {
    final l10n = AppLocalizations.of(context)!;
    final confirmed = await showDialog<bool>(
      context: context,
      builder: (dialogContext) => AlertDialog(
        title: Text(l10n.accountSessionRevokeTitle),
        content: Text(l10n.accountSessionRevokeBody),
        actions: [
          TextButton(
            onPressed: () => Navigator.of(dialogContext).pop(false),
            child: Text(l10n.accountSessionRevokeCancel),
          ),
          FilledButton(
            key: const Key('session-revoke-confirm'),
            onPressed: () => Navigator.of(dialogContext).pop(true),
            child: Text(l10n.accountSessionRevokeConfirm),
          ),
        ],
      ),
    );
    if (confirmed == true && mounted) {
      _securityBloc.add(SecurityEvent.sessionRevokeRequested(sessionId));
    }
  }

  void _setupMfa() =>
      _securityBloc.add(const SecurityEvent.mfaEnrollmentRequested());

  void _cancelMfa() {
    _mfaCodeController.clear();
    _securityBloc.add(const SecurityEvent.mfaEnrollmentCancelled());
  }

  /// Dismisses a failed MFA removal and returns the section to its idle state.
  /// The authoritative MFA-enabled state always comes from the refreshed
  /// account overview, never from local fabrication.
  void _cancelRemoveMfa() =>
      _securityBloc.add(const SecurityEvent.mfaRemovalCancelled());

  /// Retries a failed removal directly. The method id is explicit and was
  /// already confirmed by the user before the first attempt; nothing was
  /// removed, so re-confirming would be pointless friction.
  void _retryRemoveMfa(String methodId) =>
      _securityBloc.add(SecurityEvent.mfaRemovalRequested(methodId));

  void _submitMfaCode() {
    if (!(_mfaFormKey.currentState?.validate() ?? false)) return;
    _securityBloc.add(SecurityEvent.mfaEnrollmentCompleted(
      code: _mfaCodeController.text.trim(),
    ));
  }

  Future<void> _confirmRemoveMfa(
    BuildContext context,
    String methodId,
  ) async {
    final l10n = AppLocalizations.of(context)!;
    final confirmed = await showDialog<bool>(
      context: context,
      builder: (dialogContext) => AlertDialog(
        title: Text(l10n.accountMfaRemoveTitle),
        content: Text(l10n.accountMfaRemoveBody),
        actions: [
          TextButton(
            onPressed: () => Navigator.of(dialogContext).pop(false),
            child: Text(l10n.accountMfaRemoveCancel),
          ),
          FilledButton(
            key: const Key('mfa-remove-confirm'),
            onPressed: () => Navigator.of(dialogContext).pop(true),
            child: Text(l10n.accountMfaRemoveConfirm),
          ),
        ],
      ),
    );
    if (confirmed == true && mounted) {
      _securityBloc.add(SecurityEvent.mfaRemovalRequested(methodId));
    }
  }

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
    return MultiBlocProvider(
      providers: [
        BlocProvider<AccountBloc>.value(value: _bloc),
        BlocProvider<SecurityBloc>.value(value: _securityBloc),
      ],
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
        child: BlocListener<SecurityBloc, SecurityState>(
          listener: (context, state) {
            if (state is! SecurityLoaded) return;
            // Successful enrollment cleared the one-time material; the
            // authoritative MFA-enabled state comes from the refreshed /me.
            if (state.mfaStatus is MfaEnrollmentSucceeded) {
              _mfaCodeController.clear();
              context
                  .read<AccountBloc>()
                  .add(const AccountEvent.retryRequested());
            }
            // Successful removal revokes all sessions on the server.
            if (state.mfaRemovalStatus is MfaRemovalSucceeded) {
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
                      mfaFormKey: _mfaFormKey,
                      mfaCodeController: _mfaCodeController,
                      onSetupMfa: _setupMfa,
                      onCancelMfa: _cancelMfa,
                      onSubmitMfaCode: _submitMfaCode,
                      onRemoveMfa: (methodId) =>
                          _confirmRemoveMfa(context, methodId),
                      onCancelRemoveMfa: _cancelRemoveMfa,
                      onRetryRemoveMfa: _retryRemoveMfa,
                      onRetrySessions: _retrySessions,
                      onRevokeSession: (sessionId) =>
                          _confirmRevokeSession(context, sessionId),
                      onRetryRevokeSession: _retryRevokeSession,
                      onRevokeOthers: () => _confirmRevokeOthers(context),
                      onRetryRevokeOthers: _retryRevokeOthers,
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
    required this.mfaFormKey,
    required this.mfaCodeController,
    required this.onSetupMfa,
    required this.onCancelMfa,
    required this.onSubmitMfaCode,
    required this.onRemoveMfa,
    required this.onCancelRemoveMfa,
    required this.onRetryRemoveMfa,
    required this.onRetrySessions,
    required this.onRevokeSession,
    required this.onRetryRevokeSession,
    required this.onRevokeOthers,
    required this.onRetryRevokeOthers,
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
  final GlobalKey<FormState> mfaFormKey;
  final TextEditingController mfaCodeController;
  final VoidCallback onSetupMfa;
  final VoidCallback onCancelMfa;
  final VoidCallback onSubmitMfaCode;
  final ValueChanged<String> onRemoveMfa;
  final VoidCallback onCancelRemoveMfa;
  final ValueChanged<String> onRetryRemoveMfa;
  final VoidCallback onRetrySessions;
  final ValueChanged<String> onRevokeSession;
  final ValueChanged<String> onRetryRevokeSession;
  final VoidCallback onRevokeOthers;
  final VoidCallback onRetryRevokeOthers;

  @override
  Widget build(BuildContext context) {
    final securityState = context.watch<SecurityBloc>().state;
    return ListView(
      padding: const EdgeInsets.all(16),
      children: [
        _OverviewCard(account: account),
        const SizedBox(height: 16),
        _IdentitySecurityCard(
          account: account,
          securityState: securityState,
          mfaFormKey: mfaFormKey,
          mfaCodeController: mfaCodeController,
          onSetupMfa: onSetupMfa,
          onCancelMfa: onCancelMfa,
          onSubmitMfaCode: onSubmitMfaCode,
          onRemoveMfa: onRemoveMfa,
          onCancelRemoveMfa: onCancelRemoveMfa,
          onRetryRemoveMfa: onRetryRemoveMfa,
          onRetrySessions: onRetrySessions,
          onRevokeSession: onRevokeSession,
          onRetryRevokeSession: onRetryRevokeSession,
          onRevokeOthers: onRevokeOthers,
          onRetryRevokeOthers: onRetryRevokeOthers,
        ),
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
          // Ratio-based columns (no fixed widths): both sides wrap, so large
          // text scaling never lets the value overlap the label.
          Expanded(
            flex: 2,
            child: Text(
              label,
              style: const TextStyle(fontWeight: FontWeight.w600),
            ),
          ),
          const SizedBox(width: 12),
          Expanded(flex: 3, child: Text(value)),
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
                _LiveErrorText(message: errorText),
              ],
              const SizedBox(height: 16),
              SizedBox(
                width: double.infinity,
                child: FilledButton(
                  onPressed: submitting ? null : onSubmit,
                  child: submitting
                      ? Semantics(
                          label: l10n.accountPasswordChanging,
                          child: const SizedBox(
                            width: 20,
                            height: 20,
                            child: CircularProgressIndicator(strokeWidth: 2),
                          ),
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

/// Identity & Security section of the account page: MFA status + enrollment
/// flow, registered devices, and the read-only session list.
class _IdentitySecurityCard extends StatelessWidget {
  const _IdentitySecurityCard({
    required this.account,
    required this.securityState,
    required this.mfaFormKey,
    required this.mfaCodeController,
    required this.onSetupMfa,
    required this.onCancelMfa,
    required this.onSubmitMfaCode,
    required this.onRemoveMfa,
    required this.onCancelRemoveMfa,
    required this.onRetryRemoveMfa,
    required this.onRetrySessions,
    required this.onRevokeSession,
    required this.onRetryRevokeSession,
    required this.onRevokeOthers,
    required this.onRetryRevokeOthers,
  });

  final UserAccountDto account;
  final SecurityState securityState;
  final GlobalKey<FormState> mfaFormKey;
  final TextEditingController mfaCodeController;
  final VoidCallback onSetupMfa;
  final VoidCallback onCancelMfa;
  final VoidCallback onSubmitMfaCode;
  final ValueChanged<String> onRemoveMfa;
  final VoidCallback onCancelRemoveMfa;
  final ValueChanged<String> onRetryRemoveMfa;
  final VoidCallback onRetrySessions;
  final ValueChanged<String> onRevokeSession;
  final ValueChanged<String> onRetryRevokeSession;
  final VoidCallback onRevokeOthers;
  final VoidCallback onRetryRevokeOthers;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final l10n = AppLocalizations.of(context)!;
    final mfaEnabled =
        account.mfaMethods.any((m) => m.isVerified && m.isActive);
    // The only verified-active method id; used to drive the removal action.
    final activeMfaMethodId = account.mfaMethods
            .where((m) => m.isVerified && m.isActive)
            .map((m) => m.id)
            .firstOrNull ??
        '';

    // The page-scoped bloc starts in [SecurityInitial] and moves to
    // [SecurityLoaded] on the first frames; a not-yet-loaded state renders as
    // the safe idle defaults. (Instance fields don't promote, so read into a
    // local first.)
    final rawSecurity = securityState;
    final SecurityLoaded security;
    if (rawSecurity is SecurityLoaded) {
      security = rawSecurity;
    } else {
      security = const SecurityLoaded();
    }

    return Card(
      child: Padding(
        padding: const EdgeInsets.all(16),
        child: Column(
          crossAxisAlignment: CrossAxisAlignment.start,
          children: [
            Row(
              children: [
                Icon(Icons.verified_user_outlined,
                    color: theme.colorScheme.primary),
                const SizedBox(width: 8),
                Text(l10n.accountIdentitySecurityTitle,
                    style: theme.textTheme.titleMedium),
              ],
            ),
            const SizedBox(height: 12),
            _MfaSection(
              mfaStatus: security.mfaStatus,
              mfaRemovalStatus: security.mfaRemovalStatus,
              mfaEnabled: mfaEnabled,
              activeMfaMethodId: activeMfaMethodId,
              mfaFormKey: mfaFormKey,
              mfaCodeController: mfaCodeController,
              onSetupMfa: onSetupMfa,
              onCancelMfa: onCancelMfa,
              onSubmitMfaCode: onSubmitMfaCode,
              onRemoveMfa: onRemoveMfa,
              onCancelRemoveMfa: onCancelRemoveMfa,
              onRetryRemoveMfa: onRetryRemoveMfa,
            ),
            const Divider(height: 24),
            _DevicesSection(devices: account.devices),
            const Divider(height: 24),
            _SessionsSection(
              sessions: security.sessions,
              loading: security.isSessionsLoading,
              error: security.sessionsError,
              revokingSessionIds: security.revokingSessionIds,
              sessionRevokeErrors: security.sessionRevokeErrors,
              revokedSessionIds: security.revokedSessionIds,
              onRevokeSession: onRevokeSession,
              onRevokeRetry: onRetryRevokeSession,
              onRetry: onRetrySessions,
              revokingOthers: security.revokingOthers,
              revokeOthersError: security.revokeOthersError,
              revokeOthersSucceeded: security.revokeOthersSucceeded,
              onRevokeOthers: onRevokeOthers,
              onRetryRevokeOthers: onRetryRevokeOthers,
            ),
          ],
        ),
      ),
    );
  }
}

class _MfaSection extends StatelessWidget {
  const _MfaSection({
    required this.mfaStatus,
    required this.mfaRemovalStatus,
    required this.mfaEnabled,
    required this.activeMfaMethodId,
    required this.mfaFormKey,
    required this.mfaCodeController,
    required this.onSetupMfa,
    required this.onCancelMfa,
    required this.onSubmitMfaCode,
    required this.onRemoveMfa,
    required this.onCancelRemoveMfa,
    required this.onRetryRemoveMfa,
  });

  final MfaEnrollmentStatus mfaStatus;
  final MfaRemovalStatus mfaRemovalStatus;
  final bool mfaEnabled;
  final String activeMfaMethodId;
  final GlobalKey<FormState> mfaFormKey;
  final TextEditingController mfaCodeController;
  final VoidCallback onSetupMfa;
  final VoidCallback onCancelMfa;
  final VoidCallback onSubmitMfaCode;
  final ValueChanged<String> onRemoveMfa;
  final VoidCallback onCancelRemoveMfa;
  final ValueChanged<String> onRetryRemoveMfa;

  @override
  Widget build(BuildContext context) {
    // A removal in progress or completed overrides the enrollment idle view.
    if (mfaRemovalStatus is MfaRemovalInProgress) {
      return const _MfaRemoving();
    }
    if (mfaRemovalStatus is MfaRemovalSucceeded) {
      return _MfaRemovalSuccess(onRetry: onSetupMfa);
    }
    if (mfaRemovalStatus is MfaRemovalFailed) {
      return _MfaRemovalFailed(
        error: (mfaRemovalStatus as MfaRemovalFailed).error,
        methodId: (mfaRemovalStatus as MfaRemovalFailed).methodId,
        onRetry: onRetryRemoveMfa,
        onCancel: onCancelRemoveMfa,
      );
    }

    return switch (mfaStatus) {
      MfaEnrollmentStarting() => const _MfaStarting(),
      MfaEnrollmentPending(
        :final secret,
        :final provisioningUri,
      ) =>
        _MfaEnrollmentForm(
          submitting: false,
          secret: secret,
          provisioningUri: provisioningUri,
          formKey: mfaFormKey,
          codeController: mfaCodeController,
          onCancel: onCancelMfa,
          onSubmit: onSubmitMfaCode,
        ),
      MfaEnrollmentSubmitting(
        :final secret,
        :final provisioningUri,
      ) =>
        _MfaEnrollmentForm(
          submitting: true,
          secret: secret,
          provisioningUri: provisioningUri,
          formKey: mfaFormKey,
          codeController: mfaCodeController,
          onCancel: onCancelMfa,
          onSubmit: onSubmitMfaCode,
        ),
      MfaEnrollmentSucceeded() => const _MfaSucceeded(),
      MfaEnrollmentFailed(:final error) => _MfaFailed(
          error: error,
          onRetry: onSetupMfa,
          onCancel: onCancelMfa,
        ),
      MfaEnrollmentIdle() => _MfaIdle(
          mfaEnabled: mfaEnabled,
          activeMfaMethodId: activeMfaMethodId,
          onSetupMfa: onSetupMfa,
          onRemoveMfa: onRemoveMfa,
        ),
    };
  }
}

class _MfaIdle extends StatelessWidget {
  const _MfaIdle({
    required this.mfaEnabled,
    required this.activeMfaMethodId,
    required this.onSetupMfa,
    required this.onRemoveMfa,
  });

  final bool mfaEnabled;
  final String activeMfaMethodId;
  final VoidCallback onSetupMfa;
  final ValueChanged<String> onRemoveMfa;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final l10n = AppLocalizations.of(context)!;
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Row(
          children: [
            Expanded(
              child: Text(l10n.accountMfaSectionTitle,
                  style: theme.textTheme.titleSmall),
            ),
            if (mfaEnabled)
              OutlinedButton.icon(
                key: const Key('mfa-remove'),
                onPressed: () => onRemoveMfa(activeMfaMethodId),
                icon: const Icon(Icons.delete_outline),
                label: Text(l10n.accountMfaRemove),
              )
            else
              OutlinedButton.icon(
                key: const Key('mfa-setup'),
                onPressed: onSetupMfa,
                icon: const Icon(Icons.add),
                label: Text(l10n.accountMfaSetUp),
              ),
          ],
        ),
        const SizedBox(height: 4),
        Text(
          mfaEnabled ? l10n.accountMfaEnabledBody : l10n.accountMfaSetupBody,
          style: theme.textTheme.bodySmall,
        ),
      ],
    );
  }
}

class _MfaStarting extends StatelessWidget {
  const _MfaStarting();

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final l10n = AppLocalizations.of(context)!;
    return Row(
      children: [
        const SizedBox(
          width: 16,
          height: 16,
          child: CircularProgressIndicator(strokeWidth: 2),
        ),
        const SizedBox(width: 8),
        Expanded(
          child:
              Text(l10n.accountMfaPreparing, style: theme.textTheme.bodyMedium),
        ),
      ],
    );
  }
}

/// The enrollment capture step. Shows the ONE-time provisioning URI and manual
/// secret the user needs to configure an authenticator, then collects the
/// 6-digit verification code. The material is transient: it lives only in this
/// page-scoped bloc state and is cleared on success, failure, cancellation,
/// page teardown, and bloc disposal.
class _MfaEnrollmentForm extends StatelessWidget {
  const _MfaEnrollmentForm({
    required this.submitting,
    required this.secret,
    required this.provisioningUri,
    required this.formKey,
    required this.codeController,
    required this.onCancel,
    required this.onSubmit,
  });

  final bool submitting;
  final String secret;
  final String provisioningUri;
  final GlobalKey<FormState> formKey;
  final TextEditingController codeController;
  final VoidCallback onCancel;
  final VoidCallback onSubmit;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final l10n = AppLocalizations.of(context)!;
    return Container(
      padding: const EdgeInsets.all(12),
      decoration: BoxDecoration(
        border: Border.all(color: theme.colorScheme.outlineVariant),
        borderRadius: BorderRadius.circular(8),
      ),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Text(l10n.accountMfaSetupTitle, style: theme.textTheme.titleSmall),
          const SizedBox(height: 8),
          Text(l10n.accountMfaSecretWarning, style: theme.textTheme.bodySmall),
          const SizedBox(height: 12),
          _SecretValue(label: l10n.accountMfaSetupCode, value: provisioningUri),
          const SizedBox(height: 12),
          _SecretValue(label: l10n.accountMfaManualSecret, value: secret),
          const SizedBox(height: 16),
          Form(
            key: formKey,
            child: TextFormField(
              key: const Key('mfa-enroll-code'),
              controller: codeController,
              enabled: !submitting,
              keyboardType: TextInputType.number,
              maxLength: 6,
              textInputAction: TextInputAction.done,
              decoration: InputDecoration(
                labelText: l10n.accountMfaVerificationCode,
                hintText: l10n.mfaCodeHint,
                counterText: '',
                border: const OutlineInputBorder(),
              ),
              validator: (value) {
                final code = value?.trim() ?? '';
                if (code.isEmpty) return l10n.loginRequiredField;
                if (!RegExp(r'^\d{6}$').hasMatch(code)) {
                  return l10n.mfaCodeLength;
                }
                return null;
              },
              onFieldSubmitted: submitting ? null : (_) => onSubmit(),
            ),
          ),
          const SizedBox(height: 16),
          Row(
            mainAxisAlignment: MainAxisAlignment.end,
            children: [
              OutlinedButton(
                onPressed: submitting ? null : onCancel,
                child: Text(l10n.accountMfaCancel),
              ),
              const SizedBox(width: 8),
              FilledButton(
                key: const Key('mfa-enroll-verify'),
                onPressed: submitting ? null : onSubmit,
                child: submitting
                    ? Semantics(
                        label: l10n.mfaVerifying,
                        child: const SizedBox(
                          width: 18,
                          height: 18,
                          child: CircularProgressIndicator(strokeWidth: 2),
                        ),
                      )
                    : Text(l10n.accountMfaVerify),
              ),
            ],
          ),
        ],
      ),
    );
  }
}

class _SecretValue extends StatelessWidget {
  const _SecretValue({required this.label, required this.value});

  final String label;
  final String value;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Text(label, style: theme.textTheme.labelSmall),
        const SizedBox(height: 2),
        SelectableText(value, style: theme.textTheme.bodyMedium),
      ],
    );
  }
}

class _MfaSucceeded extends StatelessWidget {
  const _MfaSucceeded();

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final l10n = AppLocalizations.of(context)!;
    return Row(
      children: [
        Icon(Icons.check_circle_outline, color: theme.colorScheme.primary),
        const SizedBox(width: 8),
        Expanded(
          child: Text(l10n.accountMfaSetupSucceeded,
              style: theme.textTheme.bodyMedium),
        ),
      ],
    );
  }
}

class _MfaFailed extends StatelessWidget {
  const _MfaFailed({
    required this.error,
    required this.onRetry,
    required this.onCancel,
  });

  final AppException? error;
  final VoidCallback onRetry;
  final VoidCallback onCancel;

  @override
  Widget build(BuildContext context) {
    final l10n = AppLocalizations.of(context)!;
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        if (error != null)
          _LiveErrorText(message: exceptionMessage(context, error!)),
        const SizedBox(height: 4),
        Row(
          children: [
            TextButton.icon(
              onPressed: onRetry,
              icon: const Icon(Icons.refresh),
              label: Text(l10n.homeRetry),
            ),
            TextButton(
              onPressed: onCancel,
              child: Text(l10n.accountMfaCancel),
            ),
          ],
        ),
      ],
    );
  }
}

class _MfaRemoving extends StatelessWidget {
  const _MfaRemoving();

  @override
  Widget build(BuildContext context) {
    final l10n = AppLocalizations.of(context)!;
    return Row(
      children: [
        Semantics(
          label: l10n.accountMfaRemoving,
          child: const SizedBox(
            width: 18,
            height: 18,
            child: CircularProgressIndicator(strokeWidth: 2),
          ),
        ),
        const SizedBox(width: 8),
        Expanded(
          child: Text(l10n.accountMfaRemoving,
              style: Theme.of(context).textTheme.bodySmall),
        ),
      ],
    );
  }
}

class _MfaRemovalSuccess extends StatelessWidget {
  const _MfaRemovalSuccess({required this.onRetry});

  final VoidCallback onRetry;

  @override
  Widget build(BuildContext context) {
    final l10n = AppLocalizations.of(context)!;
    final theme = Theme.of(context);
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Semantics(
          liveRegion: true,
          child: Row(
            children: [
              Icon(Icons.check_circle_outline,
                  color: theme.colorScheme.primary),
              const SizedBox(width: 8),
              Expanded(
                child: Text(l10n.accountMfaRemoveSuccess,
                    style: theme.textTheme.bodyMedium),
              ),
            ],
          ),
        ),
        const SizedBox(height: 4),
        TextButton.icon(
          onPressed: onRetry,
          icon: const Icon(Icons.add),
          label: Text(l10n.accountMfaSetUp),
        ),
      ],
    );
  }
}

class _MfaRemovalFailed extends StatelessWidget {
  const _MfaRemovalFailed({
    required this.methodId,
    required this.error,
    required this.onRetry,
    required this.onCancel,
  });

  final String methodId;
  final AppException? error;
  final ValueChanged<String> onRetry;
  final VoidCallback onCancel;

  @override
  Widget build(BuildContext context) {
    final l10n = AppLocalizations.of(context)!;
    // The mapped/server detail is only ever secondary; the primary message is
    // a stable, localized failure text that never leaks raw exception material.
    final detail = error == null ? null : exceptionMessage(context, error);
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        _LiveErrorText(message: l10n.accountMfaRemoveFailed),
        if (detail != null && detail != l10n.accountMfaRemoveFailed)
          Padding(
            padding: const EdgeInsets.only(top: 4),
            child: Text(detail, style: Theme.of(context).textTheme.bodySmall),
          ),
        const SizedBox(height: 4),
        Row(
          children: [
            TextButton.icon(
              key: Key('mfa-remove-retry-$methodId'),
              onPressed: () => onRetry(methodId),
              icon: const Icon(Icons.refresh),
              label: Text(l10n.homeRetry),
            ),
            TextButton(
              key: const Key('mfa-remove-cancel'),
              onPressed: onCancel,
              child: Text(l10n.accountMfaRemoveCancel),
            ),
          ],
        ),
      ],
    );
  }
}

class _DevicesSection extends StatelessWidget {
  const _DevicesSection({required this.devices});

  final List<DeviceDto> devices;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final l10n = AppLocalizations.of(context)!;
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Text(l10n.accountDevicesTitle, style: theme.textTheme.titleSmall),
        const SizedBox(height: 8),
        if (devices.isEmpty)
          Padding(
            padding: const EdgeInsets.symmetric(vertical: 4),
            child: Text(l10n.accountDevicesEmpty,
                style: theme.textTheme.bodyMedium),
          )
        else
          for (final device in devices) _DeviceRow(device: device),
      ],
    );
  }
}

class _DeviceRow extends StatelessWidget {
  const _DeviceRow({required this.device});

  final DeviceDto device;

  String _title(BuildContext context) {
    final l10n = AppLocalizations.of(context)!;
    if (device.name.trim().isNotEmpty) return device.name;
    final platform = device.platform;
    if (platform != null && platform.trim().isNotEmpty) return platform;
    // Backend-provided values are used as-is; a neutral fallback label is
    // displayed only when the record carries neither a name nor a platform.
    return l10n.accountDeviceUnknown;
  }

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final l10n = AppLocalizations.of(context)!;
    final dateFormat = DateFormat.yMMMd(l10n.localeName);
    final platform = device.platform;
    final title = _title(context);
    final subtitle = (platform != null && platform.trim().isNotEmpty)
        ? '${l10n.accountDeviceRegisteredOn}: '
            '${dateFormat.format(device.registeredOn)} · $platform'
        : '${l10n.accountDeviceRegisteredOn}: '
            '${dateFormat.format(device.registeredOn)}';

    return Padding(
      padding: const EdgeInsets.symmetric(vertical: 4),
      child: Row(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Icon(Icons.devices_outlined, color: theme.colorScheme.outline),
          const SizedBox(width: 12),
          Expanded(
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                Text(title, style: theme.textTheme.bodyMedium),
                Text(subtitle, style: theme.textTheme.bodySmall),
              ],
            ),
          ),
          const SizedBox(width: 8),
          Chip(
            label: Text(
              device.isTrusted
                  ? l10n.accountDeviceTrusted
                  : l10n.accountDeviceNotTrusted,
              style: theme.textTheme.labelMedium,
            ),
          ),
        ],
      ),
    );
  }
}

class _SessionsSection extends StatelessWidget {
  const _SessionsSection({
    required this.sessions,
    required this.loading,
    required this.error,
    required this.revokingSessionIds,
    required this.sessionRevokeErrors,
    required this.revokedSessionIds,
    required this.onRevokeSession,
    required this.onRevokeRetry,
    required this.onRetry,
    required this.revokingOthers,
    required this.revokeOthersError,
    required this.revokeOthersSucceeded,
    required this.onRevokeOthers,
    required this.onRetryRevokeOthers,
  });

  final List<SessionDto> sessions;
  final bool loading;
  final AppException? error;
  final Set<String> revokingSessionIds;
  final Map<String, AppException> sessionRevokeErrors;
  final Set<String> revokedSessionIds;
  final ValueChanged<String> onRevokeSession;
  final ValueChanged<String> onRevokeRetry;
  final VoidCallback onRetry;
  final bool revokingOthers;
  final AppException? revokeOthersError;
  final bool revokeOthersSucceeded;
  final VoidCallback onRevokeOthers;
  final VoidCallback onRetryRevokeOthers;

  /// Revoke-others failure message. A 404 (or 403) renders a generic message
  /// that never reveals account/session details, matching the backend's
  /// uniform response. Stale/invalid tokens and transient failures stay
  /// retryable.
  String _revokeOthersFailureMessage(BuildContext context, AppException error) {
    final l10n = AppLocalizations.of(context)!;
    return switch (error) {
      NotFoundException() ||
      ForbiddenException() =>
        l10n.accountSessionRevokeOthersFailed,
      _ => l10n.accountSessionRevokeOthersRetryable,
    };
  }

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final l10n = AppLocalizations.of(context)!;
    return Column(
      crossAxisAlignment: CrossAxisAlignment.start,
      children: [
        Text(l10n.accountSessionsTitle, style: theme.textTheme.titleSmall),
        const SizedBox(height: 8),
        Align(
          alignment: Alignment.centerLeft,
          child: revokingOthers
              ? Semantics(
                  label: l10n.accountSessionRevokeOthersRevoking,
                  child: const SizedBox(
                    width: 20,
                    height: 20,
                    child: CircularProgressIndicator(strokeWidth: 2),
                  ),
                )
              : TextButton(
                  key: const Key('session-revoke-others'),
                  onPressed: onRevokeOthers,
                  style: TextButton.styleFrom(
                    alignment: Alignment.centerLeft,
                    padding: const EdgeInsets.symmetric(
                      horizontal: 8,
                      vertical: 8,
                    ),
                  ),
                  child: Row(
                    mainAxisSize: MainAxisSize.min,
                    crossAxisAlignment: CrossAxisAlignment.start,
                    children: [
                      const Icon(Icons.logout, size: 18),
                      const SizedBox(width: 8),
                      Flexible(
                        child: Text(
                          l10n.accountSessionRevokeOthers,
                          textAlign: TextAlign.start,
                        ),
                      ),
                    ],
                  ),
                ),
        ),
        if (revokeOthersSucceeded) ...[
          Semantics(
            container: true,
            liveRegion: true,
            child: _SectionResultRow(
              icon: Icons.check_circle_outline,
              color: theme.colorScheme.primary,
              message: l10n.accountSessionRevokeOthersSuccess,
            ),
          ),
          const SizedBox(height: 8),
        ],
        if (revokeOthersError != null) ...[
          Padding(
            padding: const EdgeInsets.only(right: 8),
            child: Column(
              crossAxisAlignment: CrossAxisAlignment.start,
              children: [
                _LiveErrorText(
                  message: _revokeOthersFailureMessage(
                    context,
                    revokeOthersError!,
                  ),
                ),
                TextButton.icon(
                  key: const Key('session-revoke-others-retry'),
                  onPressed: onRetryRevokeOthers,
                  icon: const Icon(Icons.refresh),
                  label: Text(l10n.homeRetry),
                ),
              ],
            ),
          ),
          const SizedBox(height: 8),
        ],
        if (revokedSessionIds.isNotEmpty) ...[
          Semantics(
            container: true,
            liveRegion: true,
            child: _SectionResultRow(
              icon: Icons.check_circle_outline,
              color: theme.colorScheme.primary,
              message: l10n.accountSessionRevokeSuccess,
            ),
          ),
          const SizedBox(height: 8),
        ],
        if (loading && sessions.isEmpty)
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
        else if (error != null)
          // A failed request is never rendered as an empty session list.
          _SessionsError(error: error!, onRetry: onRetry)
        else if (sessions.isEmpty)
          Padding(
            padding: const EdgeInsets.symmetric(vertical: 4),
            child: Text(l10n.accountSessionsEmpty,
                style: theme.textTheme.bodyMedium),
          )
        else ...[
          for (final session in sessions)
            _SessionRow(
              session: session,
              isRevoking: revokingSessionIds.contains(session.id),
              error: sessionRevokeErrors[session.id],
              onRevoke: () => onRevokeSession(session.id),
              onRetry: () => onRevokeRetry(session.id),
            ),
          const SizedBox(height: 8),
          Text(l10n.accountSessionsNote, style: theme.textTheme.bodySmall),
        ],
      ],
    );
  }
}

class _SessionRow extends StatelessWidget {
  const _SessionRow({
    required this.session,
    required this.isRevoking,
    required this.error,
    required this.onRevoke,
    required this.onRetry,
  });

  final SessionDto session;
  final bool isRevoking;
  final AppException? error;
  final VoidCallback onRevoke;
  final VoidCallback onRetry;

  /// Combined device label rendered from backend-supplied metadata only.
  /// Null is returned when neither a name nor a platform is present: the row
  /// then stays bare and never fabricates a device identity from [SessionDto.deviceId].
  String? _deviceLabel(SessionDto session) {
    final name = session.deviceName?.trim() ?? '';
    final platform = session.devicePlatform?.trim() ?? '';
    if (name.isEmpty && platform.isEmpty) return null;
    return [name, platform].where((part) => part.isNotEmpty).join(' · ');
  }

  /// Per-session revoke failure message. A 404 (or 403) renders a generic
  /// message that never reveals whether the session exists or belongs to
  /// another account, matching the backend's uniform response. Stale/invalid
  /// tokens and transient failures stay retryable.
  String _revokeFailureMessage(BuildContext context) {
    final l10n = AppLocalizations.of(context)!;
    return switch (error) {
      NotFoundException() ||
      ForbiddenException() =>
        l10n.accountSessionRevokeFailed,
      _ => l10n.accountSessionRevokeRetryable,
    };
  }

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final l10n = AppLocalizations.of(context)!;
    final dateFormat = DateFormat.yMMMd(l10n.localeName).add_jm();
    final active = session.isActive;
    final deviceLabel = _deviceLabel(session);

    return Padding(
      padding: const EdgeInsets.symmetric(vertical: 4),
      child: Column(
        crossAxisAlignment: CrossAxisAlignment.start,
        children: [
          Row(
            crossAxisAlignment: CrossAxisAlignment.start,
            children: [
              Icon(
                active ? Icons.laptop : Icons.laptop_outlined,
                color: active
                    ? theme.colorScheme.primary
                    : theme.colorScheme.outline,
              ),
              const SizedBox(width: 12),
              Expanded(
                child: Column(
                  crossAxisAlignment: CrossAxisAlignment.start,
                  children: [
                    Text(
                      active
                          ? l10n.accountSessionActive
                          : l10n.accountSessionInactive,
                      style: theme.textTheme.bodyMedium,
                    ),
                    if (session.isCurrent) ...[
                      const SizedBox(height: 2),
                      Semantics(
                        label: l10n.accountSessionCurrent,
                        child: Row(
                          mainAxisSize: MainAxisSize.min,
                          children: [
                            Icon(
                              Icons.check_circle_outline,
                              size: 14,
                              color: theme.colorScheme.primary,
                            ),
                            const SizedBox(width: 4),
                            Text(
                              l10n.accountSessionCurrent,
                              style: theme.textTheme.labelSmall?.copyWith(
                                color: theme.colorScheme.primary,
                              ),
                            ),
                          ],
                        ),
                      ),
                    ],
                    if (deviceLabel != null) ...[
                      const SizedBox(height: 2),
                      Text(
                        deviceLabel,
                        style: theme.textTheme.bodySmall,
                      ),
                    ],
                    Text(
                      '${l10n.accountSessionsCreated}: '
                      '${dateFormat.format(session.createdOn)}\n'
                      '${l10n.accountSessionsLastUsed}: '
                      '${dateFormat.format(session.lastUsedOn)}\n'
                      '${l10n.accountSessionsExpires}: '
                      '${dateFormat.format(session.expiresOn)}',
                      style: theme.textTheme.bodySmall,
                    ),
                  ],
                ),
              ),
              if (active && !isRevoking && error == null) ...[
                const SizedBox(width: 8),
                Tooltip(
                  message: l10n.accountSessionRevokeTooltip,
                  child: TextButton.icon(
                    key: Key('session-revoke-${session.id}'),
                    onPressed: onRevoke,
                    icon: const Icon(Icons.logout, size: 18),
                    label: Text(l10n.accountSessionRevoke),
                  ),
                ),
              ],
              if (active && isRevoking) ...[
                const SizedBox(width: 8),
                Semantics(
                  label: l10n.accountSessionRevoking,
                  child: const SizedBox(
                    width: 20,
                    height: 20,
                    child: CircularProgressIndicator(strokeWidth: 2),
                  ),
                ),
              ],
            ],
          ),
          if (error != null) ...[
            const SizedBox(height: 4),
            Padding(
              padding: const EdgeInsets.only(left: 38),
              child: Row(
                crossAxisAlignment: CrossAxisAlignment.start,
                children: [
                  Expanded(
                    child: _LiveErrorText(
                      message: _revokeFailureMessage(context),
                    ),
                  ),
                  TextButton.icon(
                    key: Key('session-revoke-retry-${session.id}'),
                    onPressed: onRetry,
                    icon: const Icon(Icons.refresh),
                    label: Text(l10n.homeRetry),
                  ),
                ],
              ),
            ),
          ],
        ],
      ),
    );
  }
}

class _SessionsError extends StatelessWidget {
  const _SessionsError({required this.error, required this.onRetry});

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

/// Successful-operation confirmation row (icon + message) used by the sessions
/// section's success banners.
class _SectionResultRow extends StatelessWidget {
  const _SectionResultRow({
    required this.icon,
    required this.color,
    required this.message,
  });

  final IconData icon;
  final Color color;
  final String message;

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    return Row(
      children: [
        Icon(icon, color: color, size: 18),
        const SizedBox(width: 8),
        Expanded(
          child: Text(message, style: theme.textTheme.bodyMedium),
        ),
      ],
    );
  }
}

/// Form-level error text announced to assistive technology as a live region so
/// a failure that appears without an explicit interaction is discoverable.
class _LiveErrorText extends StatelessWidget {
  const _LiveErrorText({required this.message});

  final String message;

  @override
  Widget build(BuildContext context) {
    return Semantics(
      container: true,
      liveRegion: true,
      child: Text(
        message,
        style: TextStyle(
          color: Theme.of(context).colorScheme.error,
        ),
      ),
    );
  }
}
