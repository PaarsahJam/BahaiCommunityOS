import 'package:freezed_annotation/freezed_annotation.dart';

part 'member_event.freezed.dart';

@freezed
sealed class MemberEvent with _$MemberEvent {
  const factory MemberEvent.homeRequested() = MemberHomeRequested;

  const factory MemberEvent.profileRequested({required String personId}) =
      MemberProfileRequested;
}
