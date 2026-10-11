import 'package:dio/dio.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:member_portal/core/error/app_exception.dart';
import 'package:member_portal/core/network/error_mapper.dart';
import 'package:member_portal/features/meetings/data/meetings_api.dart';
import 'package:member_portal/features/meetings/data/meetings_repository.dart';
import 'package:member_portal/features/meetings/domain/meetings_models.dart';
import 'package:mocktail/mocktail.dart';

class _MockAdapter extends Mock implements HttpClientAdapter {}

void main() {
  // These tests wire the real MeetingsApi (over a mocked transport adapter) and
  // the real ErrorMapper into the real MeetingsRepository, so they exercise the
  // actual client/repository contract rather than a stand-in.
  late _MockAdapter adapter;
  late MeetingsRepository repository;

  setUpAll(() {
    registerFallbackValue(RequestOptions(path: 'fallback'));
  });

  setUp(() {
    adapter = _MockAdapter();
    final dio = Dio(BaseOptions(baseUrl: 'http://api.test'));
    dio.httpClientAdapter = adapter;
    repository = MeetingsRepository(MeetingsApi(dio), const ErrorMapper());
  });

  ResponseBody body(String payload, int status) => ResponseBody.fromString(
        payload,
        status,
        headers: {
          Headers.contentTypeHeader: [Headers.jsonContentType]
        },
      );

  AppException failureOf(MeetingsListResult result) => result.fold(
        (error) => error,
        (_) => throw StateError('expected a failure result'),
      );

  group('MeetingsRepository.listMeetings (right-hand manufacturing)', () {
    test('returns the parsed meetings from a bare JSON array response',
        () async {
      when(() => adapter.fetch(any(), any(), any())).thenAnswer(
        (_) async => body(
            '[{"id": "m1", "title": "One"}, {"id": "m2", "title": "Two"}]',
            200),
      );

      final result = await repository.listMeetings(
        from: DateTime.utc(2026, 4, 1),
        to: DateTime.utc(2026, 4, 30),
        organizationUnitId: 'ou-1',
      );

      final meetings =
          result.fold((error) => fail('unexpected failure: $error'), (m) => m);
      expect(meetings.map((m) => m.id), ['m1', 'm2']);
    });

    test('forwards the supported query parameters unchanged', () async {
      when(() => adapter.fetch(any(), any(), any()))
          .thenAnswer((_) async => body('[]', 200));

      final from = DateTime.utc(2026, 4, 1);
      final to = DateTime.utc(2026, 4, 30);
      await repository.listMeetings(
        from: from,
        to: to,
        organizationUnitId: 'ou-1',
      );

      final options = verify(() => adapter.fetch(captureAny(), any(), any()))
          .captured
          .single as RequestOptions;
      expect(options.path, 'meetings');
      expect(options.queryParameters['from'], from.toIso8601String());
      expect(options.queryParameters['to'], to.toIso8601String());
      expect(options.queryParameters['organizationUnitId'], 'ou-1');
    });

    test('treats an empty JSON array as a successful empty list', () async {
      when(() => adapter.fetch(any(), any(), any()))
          .thenAnswer((_) async => body('[]', 200));

      final result = await repository.listMeetings();

      final meetings =
          result.fold((error) => fail('unexpected failure: $error'), (m) => m);
      expect(meetings, isEmpty);
    });

    test('maps a 403 problem-details response to a forbidden result',
        () async {
      when(() => adapter.fetch(any(), any(), any())).thenAnswer(
        (_) async => body('{"title": "Forbidden"}', 403),
      );

      final result = await repository.listMeetings();

      expect(failureOf(result), isA<ForbiddenException>());
    });

    test('maps a transport failure to a failed result carrying a '
        'NetworkException', () async {
      when(() => adapter.fetch(any(), any(), any())).thenThrow(
        DioException(
          requestOptions: RequestOptions(path: '/meetings'),
          type: DioExceptionType.connectionError,
        ),
      );

      final result = await repository.listMeetings();

      expect(failureOf(result), isA<NetworkException>());
    });
  });
}
