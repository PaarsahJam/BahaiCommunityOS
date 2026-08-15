namespace CommunityOS.Documents.Domain.Exceptions;

/// <summary>404 — document not found or not readable (indistinguishable).</summary>
public sealed class DocumentNotFoundException(Guid documentId)
    : Exception($"Document '{documentId}' was not found.");

/// <summary>404 — version not found.</summary>
public sealed class DocumentVersionNotFoundException(Guid documentId, int versionNumber)
    : Exception($"Version {versionNumber} of document '{documentId}' was not found.");

/// <summary>400 — lifecycle transition is not permitted.</summary>
public sealed class InvalidDocumentTransitionException(Guid documentId, string from, string to)
    : Exception($"Document '{documentId}' cannot transition from '{from}' to '{to}'.");

/// <summary>409 — deactivation is blocked by a legal/administrative hold.</summary>
public sealed class HeldDocumentDeactivationException(Guid documentId)
    : Exception($"Document '{documentId}' cannot be deactivated while a legal or administrative hold is present.");

/// <summary>403 — content is not downloadable (scan state or deactivated).</summary>
public sealed class ContentNotDownloadableException(Guid documentId, Guid versionId, string reason)
    : Exception($"Content of document '{documentId}' version '{versionId}' is not downloadable ({reason}).");

/// <summary>400 — owner or organization-scope reference is not known.</summary>
public sealed class InvalidDocumentScopeException(Guid organizationUnitId)
    : Exception($"Organization unit scope '{organizationUnitId}' is not a known unit reference.");

/// <summary>400 — duplicate reference row.</summary>
public sealed class DuplicateDocumentReferenceException(Guid documentId)
    : Exception($"A reference already exists for document '{documentId}' with the same context, entity and type.");

/// <summary>413 — upload exceeds the maximum content size.</summary>
public sealed class DocumentContentTooLargeException(long maxBytes)
    : Exception($"Content exceeds the maximum size of {maxBytes} bytes.");

/// <summary>415 — MIME type is not on the allowlist.</summary>
public sealed class UnsupportedDocumentMimeTypeException(string mimeType)
    : Exception($"Content type '{mimeType}' is not supported.");

/// <summary>500 — stored content failed the SHA-256 integrity check on download.</summary>
public sealed class DocumentIntegrityViolationException(Guid documentId, Guid versionId)
    : Exception($"Content of document '{documentId}' version '{versionId}' failed the integrity check.");