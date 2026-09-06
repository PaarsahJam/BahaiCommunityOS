import 'package:flutter/material.dart';
import 'package:flutter_test/flutter_test.dart';
import 'package:member_portal/core/error/app_exception.dart';
import 'package:member_portal/features/member/application/profile_bloc.dart';
import 'package:member_portal/features/member/data/member_dtos.dart';
import 'package:member_portal/features/member/domain/member_repository.dart';
import 'package:member_portal/features/member/presentation/profile_page.dart';
import 'package:member_portal/l10n/generated/app_localizations.dart';
import 'package:mocktail/mocktail.dart';

class _MockMemberRepository extends Mock implements MemberRepository {}

PersonDetailDto _fullDetail() => PersonDetailDto(
      id: 'p1',
      preferredName: 'Ada',
      formalName: 'Ada Lovelace',
      dateOfBirth: DateTime(1987, 4, 14),
      preferredLanguage: 'English',
      status: 'Active',
      identityAccountId: 'u1',
      profileVisibility: 'Self',
      contactVisibility: 'Self',
      dateOfBirthVisibility: 'Self',
      contactMethods: [
        const ContactMethodDto(
          id: 'c1',
          type: 'email',
          value: 'ada@example.org',
          isPreferred: true,
          visibility: 'Self',
        ),
      ],
      createdOn: DateTime(2020, 1, 1),
    );

PersonDetailDto _privacyMaskedDetail() => PersonDetailDto(
      id: 'p1',
      preferredName: 'Ada',
      status: 'Active',
      profileVisibility: 'Self',
      contactVisibility: 'None',
      dateOfBirthVisibility: 'None',
      createdOn: DateTime(2020, 1, 1),
    );

void main() {
  late _MockMemberRepository repository;

  setUp(() {
    repository = _MockMemberRepository();
  });

  Future<void> pumpFrames(WidgetTester tester, [int count = 12]) async {
    for (var i = 0; i < count; i++) {
      await tester.pump(const Duration(milliseconds: 100));
    }
  }

  Future<void> pumpProfile(WidgetTester tester) async {
    await tester.pumpWidget(
      MaterialApp(
        theme: ThemeData(useMaterial3: true),
        localizationsDelegates: AppLocalizations.localizationsDelegates,
        supportedLocales: AppLocalizations.supportedLocales,
        home: ProfilePage(
          personId: 'p1',
          createProfileBloc: () => ProfileBloc(repository),
        ),
      ),
    );
    await pumpFrames(tester);
  }

  testWidgets('renders the resolved person detail', (tester) async {
    when(() => repository.personDetail('p1'))
        .thenAnswer((_) async => _fullDetail());

    await pumpProfile(tester);

    expect(find.text('Member Profile'), findsOneWidget);
    expect(find.text('Ada'), findsOneWidget);
    expect(find.text('Ada Lovelace'), findsOneWidget);
    expect(find.text('Active'), findsOneWidget);
    expect(find.text('Preferred language: English'), findsOneWidget);
    expect(find.text('Date of birth: Apr 14, 1987'), findsOneWidget);
    expect(find.text('Contact methods'), findsOneWidget);
    expect(find.text('ada@example.org'), findsOneWidget);
    expect(find.text('email'), findsOneWidget);
    verify(() => repository.personDetail('p1')).called(1);

    await tester.pumpWidget(const SizedBox());
    await tester.pump(const Duration(milliseconds: 1));
  });

  testWidgets('privacy-masked fields render safely without manufactured values',
      (tester) async {
    when(() => repository.personDetail('p1'))
        .thenAnswer((_) async => _privacyMaskedDetail());

    await pumpProfile(tester);

    expect(find.text('Ada'), findsOneWidget);
    expect(find.text('Active'), findsOneWidget);
    expect(find.text('Contact methods'), findsOneWidget);
    expect(
      find.text('Contact details are not visible to you.'),
      findsOneWidget,
    );
    expect(find.textContaining('Date of birth'), findsNothing);
    expect(find.textContaining('Preferred language'), findsNothing);
    expect(find.text('Unknown'), findsNothing);
    expect(find.text('Hidden'), findsNothing);
    expect(find.text('Not provided'), findsNothing);

    await tester.pumpWidget(const SizedBox());
    await tester.pump(const Duration(milliseconds: 1));
  });

  testWidgets('a 403 renders the localized forbidden state with retry',
      (tester) async {
    when(() => repository.personDetail('p1')).thenThrow(
      const ForbiddenException('nope', messageKey: 'profile_forbidden'),
    );

    await pumpProfile(tester);

    expect(
      find.text("You don't have permission to view this profile."),
      findsOneWidget,
    );
    expect(find.text('Try again'), findsOneWidget);
    expect(find.text('Ada'), findsNothing);

    await tester.pumpWidget(const SizedBox());
    await tester.pump(const Duration(milliseconds: 1));
  });

  testWidgets('a 404 renders the localized not-found state with retry',
      (tester) async {
    when(() => repository.personDetail('p1')).thenThrow(
      const NotFoundException('nope', messageKey: 'profile_notFound'),
    );

    await pumpProfile(tester);

    expect(
      find.text('This profile could not be found.'),
      findsOneWidget,
    );
    expect(find.text('Try again'), findsOneWidget);
    expect(
      find.text("You don't have permission to view this profile."),
      findsNothing,
    );

    await tester.pumpWidget(const SizedBox());
    await tester.pump(const Duration(milliseconds: 1));
  });

  testWidgets('a network failure is retryable and a retry reloads the profile',
      (tester) async {
    when(() => repository.personDetail('p1'))
        .thenThrow(const NetworkException('offline'));

    await pumpProfile(tester);

    expect(find.text('offline'), findsOneWidget);
    expect(find.text('Try again'), findsOneWidget);
    expect(find.text('Ada'), findsNothing);

    when(() => repository.personDetail('p1'))
        .thenAnswer((_) async => _fullDetail());

    await tester.tap(find.text('Try again'));
    await pumpFrames(tester);

    expect(find.text('Ada'), findsOneWidget);
    expect(find.byType(CircularProgressIndicator), findsNothing);
    verify(() => repository.personDetail('p1')).called(2);

    await tester.pumpWidget(const SizedBox());
    await tester.pump(const Duration(milliseconds: 1));
  });

  testWidgets(
      'a request timeout is retryable and never becomes not-found or a '
      'permission denial', (tester) async {
    when(() => repository.personDetail('p1'))
        .thenThrow(const RequestTimeoutException('slow'));

    await pumpProfile(tester);

    expect(find.text('slow'), findsOneWidget);
    expect(find.text('Try again'), findsOneWidget);
    expect(find.byType(CircularProgressIndicator), findsNothing);
    expect(find.text('This profile could not be found.'), findsNothing);
    expect(
      find.text("You don't have permission to view this profile."),
      findsNothing,
    );
    expect(find.text('Ada'), findsNothing);

    await tester.pumpWidget(const SizedBox());
    await tester.pump(const Duration(milliseconds: 1));
  });

  testWidgets(
      'a server error is retryable and never becomes not-found or a '
      'permission denial', (tester) async {
    when(() => repository.personDetail('p1')).thenThrow(
      const ServerException('boom', statusCode: 500),
    );

    await pumpProfile(tester);

    expect(find.text('boom'), findsOneWidget);
    expect(find.text('Try again'), findsOneWidget);
    expect(find.byType(CircularProgressIndicator), findsNothing);
    expect(find.text('This profile could not be found.'), findsNothing);
    expect(
      find.text("You don't have permission to view this profile."),
      findsNothing,
    );

    await tester.pumpWidget(const SizedBox());
    await tester.pump(const Duration(milliseconds: 1));
  });

  testWidgets(
      'a retry that fails again keeps the retry surface and never leaves a '
      'permanent spinner', (tester) async {
    var calls = 0;
    when(() => repository.personDetail('p1')).thenAnswer((_) async {
      calls += 1;
      throw const NetworkException('offline');
    });

    await pumpProfile(tester);

    expect(calls, 1);
    expect(find.text('offline'), findsOneWidget);
    expect(find.text('Try again'), findsOneWidget);
    expect(find.byType(CircularProgressIndicator), findsNothing);

    await tester.tap(find.text('Try again'));
    await pumpFrames(tester);

    // Exactly one new request on retry, and a second failure lands back on
    // the error surface — never a spinner and never a phantom success.
    expect(calls, 2);
    expect(find.byType(CircularProgressIndicator), findsNothing);
    expect(find.text('offline'), findsOneWidget);
    expect(find.text('Try again'), findsOneWidget);
    expect(find.text('Ada'), findsNothing);

    await tester.pumpWidget(const SizedBox());
    await tester.pump(const Duration(milliseconds: 1));
  });
}
