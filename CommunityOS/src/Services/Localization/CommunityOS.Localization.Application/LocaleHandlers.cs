using CommunityOS.Authorization.Application.Authorization;
using CommunityOS.Localization.Application.Permissions;
using CommunityOS.Localization.Domain;
using CommunityOS.Localization.Domain.Exceptions;
using MediatR;

namespace CommunityOS.Localization.Application;

public sealed record LocaleDto(
    Guid Id, string Code, string? DisplayName, string Status, bool IsDefault,
    DateTime CreatedOn, DateTime UpdatedOn);

public sealed record ListLocales(Guid ActorId) : IRequest<IReadOnlyList<LocaleDto>>;

public sealed class ListLocalesHandler(
    ILocalizationReader reader,
    AuthorizationGuard guard)
    : IRequestHandler<ListLocales, IReadOnlyList<LocaleDto>>
{
    public async Task<IReadOnlyList<LocaleDto>> Handle(ListLocales request, CancellationToken cancellationToken)
    {
        if (request.ActorId == Guid.Empty) throw new UnauthorizedAccessException("Authentication required.");
        await guard.RequireAsync(request.ActorId, LocalizationPermissions.LocaleRead, ct: cancellationToken);

        var locales = await reader.ListLocalesAsync(cancellationToken);
        return locales
            .Select(ToDto)
            .ToList();
    }

    internal static LocaleDto ToDto(Locale locale) =>
        new(locale.Id, locale.Code, locale.DisplayName,
            locale.Status.ToString().ToLowerInvariant(),
            locale.IsDefault, locale.CreatedOn, locale.UpdatedOn);
}

public sealed record CreatedLocaleDto(
    Guid Id, string Code, string? DisplayName, string Status, bool IsDefault);

public sealed record CreateLocaleCommand(
    Guid ActorId, string Code, string? DisplayName) : IRequest<CreatedLocaleDto>;

/// <summary>Registers a culture in the locale registry. Registration alone
/// makes nothing resolvable â€” activation is an explicit follow-up command
/// (ADR-029 decision 3).</summary>
public sealed class CreateLocaleHandler(
    ILocalizationReader reader,
    ILocalizationJournal journal,
    AuthorizationGuard guard)
    : IRequestHandler<CreateLocaleCommand, CreatedLocaleDto>
{
    public async Task<CreatedLocaleDto> Handle(CreateLocaleCommand command, CancellationToken cancellationToken)
    {
        if (command.ActorId == Guid.Empty) throw new UnauthorizedAccessException("Authentication required.");
        await guard.RequireAsync(command.ActorId, LocalizationPermissions.LocaleManage, ct: cancellationToken);

        var code = Bcp47.Normalize(command.Code);
        if (!Bcp47.IsValid(code))
        {
            throw new LocalizationConflictException($"'{code}' is not a valid BCP-47 culture code.");
        }

        if (await reader.LocaleCodeExistsAsync(code, cancellationToken))
        {
            throw new LocalizationConflictException($"Locale '{code}' is already registered.");
        }

        var locale = Locale.Register(code, command.DisplayName, command.ActorId, DateTime.UtcNow);
        await journal.SaveLocaleAsync(locale, previousDefault: null, cancellationToken);
        return new(locale.Id, locale.Code, locale.DisplayName,
            locale.Status.ToString().ToLowerInvariant(), locale.IsDefault);
    }
}

public sealed record ChangeLocaleStatusResult(
    Guid Id, string Code, string Status, bool IsDefault);

public sealed record ActivateLocaleCommand(Guid ActorId, string Code) : IRequest<ChangeLocaleStatusResult>;

public sealed class ActivateLocaleHandler(
    ILocalizationReader reader,
    ILocalizationJournal journal,
    AuthorizationGuard guard)
    : IRequestHandler<ActivateLocaleCommand, ChangeLocaleStatusResult>
{
    public async Task<ChangeLocaleStatusResult> Handle(ActivateLocaleCommand command, CancellationToken cancellationToken)
    {
        if (command.ActorId == Guid.Empty) throw new UnauthorizedAccessException("Authentication required.");
        await guard.RequireAsync(command.ActorId, LocalizationPermissions.LocaleManage, ct: cancellationToken);

        var locale = await reader.FindLocaleAsync(Bcp47.Normalize(command.Code), cancellationToken)
                     ?? throw new LocalizationNotFoundException("The requested locale was not found.");

        locale.Activate(DateTime.UtcNow);
        await journal.SaveLocaleAsync(locale, previousDefault: null, cancellationToken);
        return new(locale.Id, locale.Code,
            locale.Status.ToString().ToLowerInvariant(), locale.IsDefault);
    }
}

public sealed record DeactivateLocaleCommand(Guid ActorId, string Code) : IRequest<ChangeLocaleStatusResult>;

public sealed class DeactivateLocaleHandler(
    ILocalizationReader reader,
    ILocalizationJournal journal,
    AuthorizationGuard guard)
    : IRequestHandler<DeactivateLocaleCommand, ChangeLocaleStatusResult>
{
    public async Task<ChangeLocaleStatusResult> Handle(DeactivateLocaleCommand command, CancellationToken cancellationToken)
    {
        if (command.ActorId == Guid.Empty) throw new UnauthorizedAccessException("Authentication required.");
        await guard.RequireAsync(command.ActorId, LocalizationPermissions.LocaleManage, ct: cancellationToken);

        var locale = await reader.FindLocaleAsync(Bcp47.Normalize(command.Code), cancellationToken)
                     ?? throw new LocalizationNotFoundException("The requested locale was not found.");

        locale.Deactivate(DateTime.UtcNow);
        await journal.SaveLocaleAsync(locale, previousDefault: null, cancellationToken);
        return new(locale.Id, locale.Code,
            locale.Status.ToString().ToLowerInvariant(), locale.IsDefault);
    }
}
