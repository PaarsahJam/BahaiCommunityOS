using CommunityOS.Authorization.Application.Authorization;
using CommunityOS.Localization.Application.Permissions;
using CommunityOS.Localization.Domain;
using CommunityOS.Localization.Domain.Exceptions;
using MediatR;

namespace CommunityOS.Localization.Application;

public sealed record NamespaceDto(
    Guid Id, string Name, string? Description, DateTime CreatedOn);

public sealed record ListNamespaces(Guid ActorId) : IRequest<IReadOnlyList<NamespaceDto>>;

public sealed class ListNamespacesHandler(
    ILocalizationReader reader,
    AuthorizationGuard guard)
    : IRequestHandler<ListNamespaces, IReadOnlyList<NamespaceDto>>
{
    public async Task<IReadOnlyList<NamespaceDto>> Handle(ListNamespaces request, CancellationToken cancellationToken)
    {
        if (request.ActorId == Guid.Empty) throw new UnauthorizedAccessException("Authentication required.");
        await guard.RequireAsync(request.ActorId, LocalizationPermissions.ResourceRead, ct: cancellationToken);

        var namespaces = await reader.ListNamespacesAsync(cancellationToken);
        return namespaces
            .Select(n => new NamespaceDto(n.Id, n.Name, n.Description, n.CreatedOn))
            .ToList();
    }
}

public sealed record CreateNamespaceCommand(
    Guid ActorId, string Name, string? Description) : IRequest<NamespaceDto>;

/// <summary>Creates a resource namespace. Reserved prefixes (the
/// Knowledge/Library boundary) are rejected by the domain (ADR-029 decision
/// 18). Requires propose: namespace creation is part of authoring new
/// translatable surface, not catalog administration.</summary>
public sealed class CreateNamespaceHandler(
    ILocalizationReader reader,
    ILocalizationJournal journal,
    AuthorizationGuard guard)
    : IRequestHandler<CreateNamespaceCommand, NamespaceDto>
{
    public async Task<NamespaceDto> Handle(CreateNamespaceCommand command, CancellationToken cancellationToken)
    {
        if (command.ActorId == Guid.Empty) throw new UnauthorizedAccessException("Authentication required.");
        await guard.RequireAsync(command.ActorId, LocalizationPermissions.ResourcePropose, ct: cancellationToken);

        if (await reader.FindNamespaceByNameAsync(command.Name.Trim().ToLowerInvariant(), cancellationToken) is not null)
        {
            throw new LocalizationConflictException(
                $"A resource namespace '{command.Name.Trim().ToLowerInvariant()}' already exists.");
        }

        var @namespace = ResourceNamespace.Create(
            command.Name, command.Description, command.ActorId, DateTime.UtcNow);
        await journal.SaveNamespaceAsync(@namespace, cancellationToken);
        return new(@namespace.Id, @namespace.Name, @namespace.Description, @namespace.CreatedOn);
    }
}
