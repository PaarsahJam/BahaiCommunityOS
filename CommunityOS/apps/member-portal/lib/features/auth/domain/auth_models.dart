import 'package:freezed_annotation/freezed_annotation.dart';

import '../data/auth_dtos.dart' show UserAccountDto;

export '../../../core/error/app_exception.dart';

part 'auth_models.freezed.dart';

/// Identity projection of a signed-in member.
class AuthUser {
  const AuthUser({
    required this.userAccountId,
    required this.email,
    this.status,
    this.verifiedOn,
  });

  factory AuthUser.fromUserAccount(UserAccountDto account) => AuthUser(
        userAccountId: account.id,
        email: account.email,
        status: account.status,
        verifiedOn: account.verifiedOn,
      );

  final String userAccountId;
  final String email;
  final String? status;
  final DateTime? verifiedOn;
}

/// Result of submitting credentials to the Identity service.
@freezed
sealed class AuthLoginResult with _$AuthLoginResult {
  /// The account has at least one active MFA method; credentials were not
  /// accepted and no tokens were issued.
  const factory AuthLoginResult.requiresMfa({
    required String accountId,
    required String email,
  }) = AuthRequiresMfa;

  /// Tokens were issued and persisted.
  const factory AuthLoginResult.authenticated({required AuthUser user}) =
      AuthSignedIn;
}
