using CommunityOS.Documents.Application.DTOs;
using CommunityOS.Documents.Domain.Aggregates;

namespace CommunityOS.Documents.Application;

internal static class DocumentsMappingExtensions
{
    internal static DocumentDto ToDto(this Document d) =>
        new(d.Id,
            d.Title,
            d.Description,
            d.Status.Name,
            d.OwnerId is { } ownerId && d.OwnerType is { } ownerType
                ? new OwnerReferenceDto(ownerType, ownerId)
                : null,
            d.OrganizationUnitId,
            d.AllOrganizationUnitIds.Except(new[] { d.OrganizationUnitId!.Value })
                .ToArray()
                .AsReadOnly(),
            new DocumentClassificationDto(
                d.Classification.ClassificationCode,
                d.Classification.IsSensitive,
                d.Classification.RetentionCategory,
                d.Classification.LegalHoldReference,
                d.Classification.AdministrativeHoldReference),
            d.CurrentVersion?.ToDto(),
            d.CreatedBy,
            d.CreatedOn,
            d.UpdatedBy,
            d.UpdatedOn);

    internal static DocumentSummaryDto ToSummaryDto(this Document d) =>
        new(d.Id,
            d.Title,
            d.Status.Name,
            d.Classification.ClassificationCode,
            d.Classification.IsSensitive,
            d.OrganizationUnitId,
            d.CurrentVersion?.VersionNumber,
            d.UpdatedOn);

    internal static DocumentVersionDto ToDto(this DocumentVersion v) =>
        new(v.Id,
            v.VersionNumber,
            v.MimeType,
            v.SizeBytes,
            v.FileName,
            v.ContentHash,
            v.UploadedBy,
            v.UploadedOn,
            v.Source,
            v.ScanStatus.Name);

    internal static DocumentReferenceDto ToDto(this DocumentReference r) =>
        new(r.Id, r.SourceContext, r.SourceEntityId, r.ReferenceType, r.CreatedBy, r.CreatedOn);

    internal static OrganizationUnitReferenceDto ToDto(this OrganizationUnitReference r) =>
        new(r.Id, r.OrganizationId, r.Name, r.UnitType, r.ParentId, r.LastSeenOn);
}