using CommunityOS.Knowledge.Application.DTOs;
using CommunityOS.Knowledge.Domain.Aggregates;
using CommunityOS.Knowledge.Domain.Entities;

namespace CommunityOS.Knowledge.Application;

internal static class KnowledgeMappingExtensions
{
    internal static WorkDto ToDto(this Work w, IReadOnlyList<Edition> editions) =>
        new(w.Id,
            w.Title,
            w.WorkType,
            w.OriginalLanguage,
            w.DefaultLanguage,
            editions.Select(e => e.ToDto()).ToList().AsReadOnly());

    internal static EditionDto ToDto(this Edition e) =>
        new(e.Id,
            e.WorkId,
            e.Language,
            e.Translator,
            e.Publisher,
            e.EditionYear,
            e.Verified);

    internal static PassageDto ToDto(this Passage p) =>
        new(p.Id,
            p.EditionId,
            p.ReferencePath,
            p.Text,
            p.SortOrder,
            p.Revision);

    internal static CategoryDto ToDto(this Category c) =>
        new(c.Id, c.Name, c.Description);

    internal static TopicDto ToDto(this Topic t) =>
        new(t.Id, t.Name, t.Description);

    internal static QuestionDto ToDto(this Question q) =>
        new(q.Id,
            q.Title,
            q.Body,
            q.Status.Name,
            q.AuthorId,
            q.CategoryId,
            q.OrganizationUnitId,
            q.AcceptedAnswerId,
            q.MergedOntoQuestionId,
            q.CreatedOn,
            q.Tags.Select(t => t.Name).ToList().AsReadOnly(),
            q.LifecycleEvents.Select(e => e.ToDto()).ToList().AsReadOnly());

    internal static QuestionLifecycleEventDto ToDto(this QuestionLifecycleEvent e) =>
        new(e.FromStatus, e.ToStatus, e.ActorId, e.OccurredOn);

    internal static AnswerDto ToDto(this Answer a) =>
        new(a.Id,
            a.QuestionId,
            a.AuthorId,
            a.Body,
            a.Source.Name,
            a.ModelId,
            a.Accepted,
            a.Revision);

    internal static DiscussionDto ToDto(this Discussion d) =>
        new(d.Id,
            d.QuestionId,
            d.Title,
            d.Status.Name,
            d.AuthorId,
            d.CreatedOn,
            d.Comments.Select(c => c.ToDto()).ToList().AsReadOnly());

    internal static CommentDto ToDto(this Comment c) =>
        new(c.Id, c.AuthorId, c.Body, c.CreatedOn);

    internal static AiSuggestionDto ToDto(this AiSuggestion s) =>
        new(s.Id,
            s.QuestionId,
            s.Body,
            s.ModelId,
            s.PromptVersion,
            s.ReviewState.Name,
            s.RequestedOn,
            s.ReviewedOn);

    internal static OrganizationUnitReferenceDto ToDto(this OrganizationUnitReference r) =>
        new(r.Id, r.OrganizationId, r.Name, r.UnitType, r.ParentId, r.LastSeenOn);

    internal static CitationDto ToCitationDto(this Passage p) =>
        new(p.Id, p.ReferencePath, p.Text, p.EditionId);
}