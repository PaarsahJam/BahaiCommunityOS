import 'package:flutter_secure_storage/flutter_secure_storage.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:member_portal/core/storage/token_storage.dart';
import 'package:mocktail/mocktail.dart';

class _MockSecureStorage extends Mock implements FlutterSecureStorage {}

void main() {
  late _MockSecureStorage secure;
  late SecureTokenStorage storage;
  late Map<String, String?> values;

  String keyOf(Invocation invocation, Symbol name) =>
      invocation.namedArguments[name] as String;

  setUp(() {
    secure = _MockSecureStorage();
    storage = SecureTokenStorage(secure);
    values = <String, String?>{};

    when(() => secure.read(key: any(named: 'key'))).thenAnswer(
      (inv) async => values[keyOf(inv, #key)],
    );
    when(() => secure.write(key: any(named: 'key'), value: any(named: 'value')))
        .thenAnswer((inv) async {
      values[keyOf(inv, #key)] = inv.namedArguments[#value] as String?;
    });
    when(() => secure.delete(key: any(named: 'key'))).thenAnswer((inv) async {
      values.remove(keyOf(inv, #key));
    });
  });

  group('SecureTokenStorage', () {
    test('returns null when any part of the pair is missing', () async {
      values['auth.access_token'] = 'a';
      values['auth.refresh_token'] = 'r';
      expect(await storage.read(), isNull);
    });

    test('writes all three parts of a token pair', () async {
      await storage.write(TokenPair(
        accessToken: 'access-1',
        refreshToken: 'refresh-1',
        expiresAt: tokenExpiry,
      ));

      expect(values['auth.access_token'], 'access-1');
      expect(values['auth.refresh_token'], 'refresh-1');
      expect(values['auth.token_expires_at'], tokenExpiry.toIso8601String());
    });

    test('round-trips a full token pair', () async {
      await storage.write(TokenPair(
        accessToken: 'access-1',
        refreshToken: 'refresh-1',
        expiresAt: tokenExpiry,
      ));

      final restored = await storage.read();

      expect(restored!.accessToken, 'access-1');
      expect(restored.refreshToken, 'refresh-1');
      expect(restored.expiresAt, tokenExpiry);
    });

    test('returns null when the expiry value is not parseable', () async {
      values['auth.access_token'] = 'a';
      values['auth.refresh_token'] = 'r';
      values['auth.token_expires_at'] = 'not-a-date';
      expect(await storage.read(), isNull);
    });

    test('clear removes every stored key', () async {
      await storage.write(TokenPair(
        accessToken: 'access-1',
        refreshToken: 'refresh-1',
        expiresAt: tokenExpiry,
      ));
      expect(values.values.where((v) => v != null), isNotEmpty);

      await storage.clear();

      expect(values.values.where((v) => v != null), isEmpty);
    });
  });
}

final tokenExpiry = DateTime(2030, 1, 1);
