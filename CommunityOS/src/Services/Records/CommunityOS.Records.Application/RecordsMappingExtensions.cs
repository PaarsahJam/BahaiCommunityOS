using CommunityOS.Records.Application.DTOs;
using CommunityOS.Records.Domain.Aggregates;

namespace CommunityOS.Records.Application;

internal static class RecordsMappingExtensions
{
    internal static RecordDto ToDto(this Record r) =>
        new(r.Id,
            r.Category,
            r.Status.Name,
            new RecordSubjectDto(r.SubjectType, r.SubjectId),
            r.OrganizationUnitId,
            r.AllOrganizationUnitIds.Except(
                    r.OrganizationUnitId is { } organizationUnitId
                        ? new[] { organizationUnitId }
                        : Array.Empty<Guid>())
                .ToArray()
                .AsReadOnly(),
            new RecordClassificationDto(
                r.Classification.ClassificationCode,
                r.Classification.IsSensitive,
                r.Classification.RetentionScheduleCode,
                r.Classification.RetentionExpiredOn),
            r.CurrentVersion?.ToDescriptorDto(),
            r.Evidence.Select(e => e.ToDto()).ToArray().AsReadOnly(),
            r.Holds.Select(h => h.ToDto(r.Id)).ToArray().AsReadOnly(),
            r.CreatedBy,
            r.CreatedOn,
            r.UpdatedBy,
            r.UpdatedOn,
            r.VerifiedBy,
            r.VerifiedOn);

    internal static RecordSummaryDto ToSummaryDto(this Record r) =>
        new(r.Id,
            r.Category,
            r.Status.Name,
            r.SubjectType,
            r.OrganizationUnitId,
            r.CurrentVersion?.VersionNumber,
            r.UpdatedOn);

    internal static RecordFieldDto ToDto(this RecordFieldValue f) =>
        new(f.FieldKey, f.FieldValue, f.IsSensitive);

    internal static RecordVersionDescriptorDto ToDescriptorDto(this RecordVersion v) =>
        new(v.Id,
            v.VersionNumber,
            v.SupersedesVersionNumber,
            v.AppliedBy,
            v.AppliedOn,
            v.ChangeReason,
            v.Fields.Count);

    internal static RecordVersionDto ToDto(this RecordVersion v, bool nonSensitiveOnly) =>
        new(v.Id,
            v.VersionNumber,
            v.SupersedesVersionNumber,
            v.AppliedBy,
            v.AppliedOn,
            v.ChangeReason,
            v.Fields
                .Where(f => !nonSensitiveOnly || !f.IsSensitive)
                .Select(f => f.ToDto())
                .ToArray()
                .AsReadOnly());

    internal static RecordEvidenceReferenceDto ToDto(this RecordEvidenceReference e) =>
        new(e.Id, e.DocumentId, e.VersionNumber, e.ReferenceType, e.AttachedBy, e.AttachedOn);

    internal static RecordHoldDto ToDto(this RecordHold h, Guid recordId, bool exposeReason = false) =>
        new(h.Id,
            h.HoldType,
            h.IsActive ? "active" : "released",
            recordId,
            h.PlacedBy,
            h.PlacedOn,
            h.ReleasedBy,
            h.ReleasedOn,
            h.DocumentReferences
                .Select(d => new RecordHoldDocumentReferenceDto(d.DocumentId, d.VersionNumber))
                .ToArray()
                .AsReadOnly(),
            exposeReason ? h.Reason : null);

    internal static RetentionScheduleDto ToDto(this RetentionSchedule s) =>
        new(s.Code,
            s.DisplayName,
            s.Description,
            s.IsRetired,
            s.Rules
                .Select(r => new RetentionRuleDto(r.Category, r.Period, r.StartTrigger, r.Disposition, r.Note, r.MaximumPeriod))
                .ToArray()
                .AsReadOnly(),
            s.CreatedBy,
            s.CreatedOn);

    internal static RecordCategoryDto ToDto(this RecordCategory c) =>
        new(c.Code, c.DisplayName, c.Description, c.IsRetired, c.CreatedBy, c.CreatedOn);

    internal static OrganizationUnitReferenceDto ToDto(this OrganizationUnitReference r) =>
        new(r.OrganizationUnitId, r.CreatedOn);
}