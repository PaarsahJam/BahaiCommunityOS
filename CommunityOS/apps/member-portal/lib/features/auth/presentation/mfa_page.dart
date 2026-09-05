import 'package:flutter/material.dart';
import 'package:flutter_bloc/flutter_bloc.dart';
import 'package:go_router/go_router.dart';
import 'package:member_portal/l10n/generated/app_localizations.dart';

import '../../../core/ui/exception_message.dart';
import '../application/auth_bloc.dart';
import '../application/auth_event.dart';
import '../application/auth_state.dart';

class MfaPage extends StatefulWidget {
  const MfaPage({super.key});

  @override
  State<MfaPage> createState() => _MfaPageState();
}

class _MfaPageState extends State<MfaPage> {
  final _formKey = GlobalKey<FormState>();
  final _codeController = TextEditingController();
  bool _attempted = false;

  @override
  void dispose() {
    _codeController.dispose();
    super.dispose();
  }

  void _submit() {
    setState(() => _attempted = true);
    if (!_formKey.currentState!.validate()) return;
    final state = context.read<AuthBloc>().state;
    if (state is! AuthMfaRequired) return;
    context.read<AuthBloc>().add(
          AuthEvent.mfaCodeSubmitted(
            email: state.email,
            mfaCode: _codeController.text.trim(),
          ),
        );
  }

  @override
  Widget build(BuildContext context) {
    final theme = Theme.of(context);
    final l10n = AppLocalizations.of(context)!;
    final state = context.watch<AuthBloc>().state;
    final isMfa = state is AuthMfaRequired;
    final email = isMfa ? state.email : '';
    final error = isMfa ? state.error : null;
    final submitting = state is AuthAuthenticating;

    return Scaffold(
      appBar: AppBar(
        leading: BackButton(
          onPressed: submitting ? null : () => context.go('/login'),
        ),
      ),
      body: SafeArea(
        child: Center(
          child: SingleChildScrollView(
            padding: const EdgeInsets.symmetric(horizontal: 24, vertical: 32),
            child: ConstrainedBox(
              constraints: const BoxConstraints(maxWidth: 420),
              child: Form(
                key: _formKey,
                autovalidateMode: _attempted
                    ? AutovalidateMode.onUserInteraction
                    : AutovalidateMode.disabled,
                child: Column(
                  mainAxisSize: MainAxisSize.min,
                  crossAxisAlignment: CrossAxisAlignment.stretch,
                  children: [
                    Text(
                      l10n.mfaTitle,
                      style: theme.textTheme.headlineMedium,
                      textAlign: TextAlign.center,
                    ),
                    const SizedBox(height: 8),
                    Text(
                      '${l10n.mfaSubtitle}\n$email',
                      style: theme.textTheme.bodyMedium,
                      textAlign: TextAlign.center,
                    ),
                    const SizedBox(height: 32),
                    TextFormField(
                      controller: _codeController,
                      enabled: !submitting,
                      keyboardType: TextInputType.number,
                      maxLength: 6,
                      textInputAction: TextInputAction.done,
                      textAlign: TextAlign.center,
                      style: theme.textTheme.headlineSmall,
                      decoration: InputDecoration(
                        labelText: l10n.mfaCode,
                        hintText: l10n.mfaCodeHint,
                        counterText: '',
                        border: const OutlineInputBorder(),
                      ),
                      validator: (value) {
                        final code = value?.trim() ?? '';
                        if (code.isEmpty) return l10n.loginRequiredField;
                        if (!RegExp(r'^\d{4,8}$').hasMatch(code)) {
                          return l10n.mfaInvalidCode;
                        }
                        return null;
                      },
                      onFieldSubmitted: (_) => _submit(),
                    ),
                    if (error != null) ...[
                      const SizedBox(height: 16),
                      Text(
                        exceptionMessage(context, error),
                        style: TextStyle(color: theme.colorScheme.error),
                        textAlign: TextAlign.center,
                      ),
                    ],
                    const SizedBox(height: 24),
                    SizedBox(
                      height: 48,
                      child: FilledButton(
                        onPressed: submitting ? null : _submit,
                        child: submitting
                            ? const SizedBox(
                                width: 22,
                                height: 22,
                                child:
                                    CircularProgressIndicator(strokeWidth: 2.5),
                              )
                            : Text(l10n.mfaSubmit),
                      ),
                    ),
                    TextButton(
                      onPressed: submitting ? null : () => context.go('/login'),
                      child: Text(l10n.mfaBackToLogin),
                    ),
                  ],
                ),
              ),
            ),
          ),
        ),
      ),
    );
  }
}
