namespace CommunityOS.Identity.Domain.Exceptions;

public sealed class MemberNotFoundException(Guid memberId)
    : Exception($"Member '{memberId}' was not found.");

public sealed class DuplicateEmailException(string email)
    : Exception($"A member with email '{email}' already exists.");

public sealed class InvalidMemberStateException(string message)
    : Exception(message);
