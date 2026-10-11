import 'package:dio/dio.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:member_portal/features/meetings/data/meetings_api.dart';
import 'package:mocktail/mocktail.dart';

class _MockAdapter extends Mock implements HttpClientAdapter {}

void main() {
  group('MeetingsApi (wire contract)', () {
    late _MockAdapter adapter;
    late MeetingsApi api;

    setUpAll(() {
      registerFallbackValue(RequestOptions(path: 'fallback'));
    });

    setUp(() {
      adapter = _MockAdapter();
      final dio = Dio(BaseOptions(baseUrl: 'http://api.test'));
      dio.httpClientAdapter = adapter;
      api = MeetingsApi(dio);
    });

    ResponseBody jsonBody(String body) => ResponseBody.fromString(
          body,
          200,
          headers: {
            Headers.contentTypeHeader: [Headers.jsonContentType]
          },
        );

    test(
        'listMeetings decodes the backend bare JSON array and maps the '
        'supported query parameters', () async {
      // The backend serializes IReadOnlyList<MeetingDto> directly: the whole
      // response body is the array, with no wrapper object.
      when(() => adapter.fetch(any(), any(), any())).thenAnswer(
        (_) async => jsonBody('''
          [{
            "id": "m1",
            "title": "Feast of Ridvan",
            "description": "Community gathering",
            "startsAt": "2026-04-21T18:00:00Z",
            "endsAt": "2026-04-21T20:00:00Z",
            "timeZone": "UTC",
            "location": "Centre",
            "organizerPersonId": "p1",
            "organizationUnitId": "ou-1",
            "status": "scheduled",
            "visibility": "members"
          }]
        '''),
      );

      final from = DateTime.utc(2026, 4, 1);
      final to = DateTime.utc(2026, 4, 30);
      final meetings = await api.listMeetings(
        from: from,
        to: to,
        organizationUnitId: 'ou-1',
      );

      final options = verify(() => adapter.fetch(captureAny(), any(), any()))
          .captured
          .single as RequestOptions;
      expect(options.method, 'GET');
      expect(options.path, 'meetings');
      expect(options.queryParameters['from'], from.toIso8601String());
      expect(options.queryParameters['to'], to.toIso8601String());
      expect(options.queryParameters['organizationUnitId'], 'ou-1');

      expect(meetings, hasLength(1));
      final meeting = meetings.single;
      expect(meeting.id, 'm1');
      expect(meeting.title, 'Feast of Ridvan');
      expect(meeting.description, 'Community gathering');
      expect(meeting.startsAt, DateTime.parse('2026-04-21T18:00:00Z'));
      expect(meeting.endsAt, DateTime.parse('2026-04-21T20:00:00Z'));
      expect(meeting.timeZone, 'UTC');
      expect(meeting.location, 'Centre');
      expect(meeting.organizerPersonId, 'p1');
      expect(meeting.organizationUnitId, 'ou-1');
      expect(meeting.status, 'scheduled');
      expect(meeting.visibility, 'members');
    });

    test('listMeetings parses every element of a bare JSON array', () async {
      when(() => adapter.fetch(any(), any(), any())).thenAnswer(
        (_) async => jsonBody(
            '[{"id": "m1", "title": "One"}, {"id": "m2", "title": "Two"}]'),
      );

      final meetings = await api.listMeetings();

      expect(meetings.map((m) => m.id), ['m1', 'm2']);
    });

    test('listMeetings returns an empty list for an empty JSON array',
        () async {
      when(() => adapter.fetch(any(), any(), any()))
          .thenAnswer((_) async => jsonBody('[]'));

      expect(await api.listMeetings(), isEmpty);
    });

    test('getMeeting decodes a single meeting object (detail is not a list)',
        () async {
      when(() => adapter.fetch(any(), any(), any())).thenAnswer(
        (_) async => jsonBody('{"id": "m1", "title": "One"}'),
      );

      final meeting = await api.getMeeting(id: 'm1');

      final options = verify(() => adapter.fetch(captureAny(), any(), any()))
          .captured
          .single as RequestOptions;
      expect(options.method, 'GET');
      expect(options.path, 'meetings/m1');
      expect(meeting.id, 'm1');
    });

    test('throws whenever the adapter errors (never fabricates data)',
        () async {
      when(() => adapter.fetch(any(), any(), any())).thenThrow(
        DioException(
          requestOptions: RequestOptions(path: '/meetings'),
          type: DioExceptionType.connectionError,
        ),
      );

      expect(api.listMeetings(), throwsA(isA<DioException>()));
    });
  });
}
