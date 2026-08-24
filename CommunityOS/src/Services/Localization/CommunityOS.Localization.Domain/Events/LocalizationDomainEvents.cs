using CommunityOS.SharedKernel.Domain.Events;

namespace CommunityOS.Localization.Domain.Events;

/// <summary>
/// In-process domain event raised by catalog-publishing transitions;
/// forwarded to the integration contract by the infrastructure publisher.
/// Payload mirrors the ratified contract shape exactly: codes, a version and
/// a timestamp only — never translation values or other catalog content.
/// </summary>
public sealed record CatalogChangedDomainEvent(
    string CatalogContext,
    string? Namespace,
    string Culture,
    long BundleVersion) : DomainEvent;
