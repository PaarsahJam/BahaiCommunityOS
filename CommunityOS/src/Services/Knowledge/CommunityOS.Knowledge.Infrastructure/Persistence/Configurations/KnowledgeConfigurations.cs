using CommunityOS.Knowledge.Domain.Aggregates;
using CommunityOS.Knowledge.Domain.Entities;
using CommunityOS.Knowledge.Domain.Enumerations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CommunityOS.Knowledge.Infrastructure.Persistence.Configurations;

internal sealed class WorkConfiguration : IEntityTypeConfiguration<Work>
{
    public void Configure(EntityTypeBuilder<Work> builder)
    {
        builder.ToTable("works");
        builder.HasKey(w => w.Id);
        builder.Property(w => w.Id).ValueGeneratedNever();

        builder.Property(w => w.Title).HasColumnName("title").HasMaxLength(300).IsRequired();
        builder.Property(w => w.WorkType).HasColumnName("work_type").HasMaxLength(50).IsRequired();
        builder.Property(w => w.OriginalLanguage).HasColumnName("original_language").HasMaxLength(20).IsRequired();
        builder.Property(w => w.DefaultLanguage).HasColumnName("default_language").HasMaxLength(20).IsRequired();

        builder.Ignore(w => w.DomainEvents);
    }
}

internal sealed class EditionConfiguration : IEntityTypeConfiguration<Edition>
{
    public void Configure(EntityTypeBuilder<Edition> builder)
    {
        builder.ToTable("editions");
        builder.HasKey(e => e.Id);
        builder.Property(e => e.Id).ValueGeneratedNever();

        builder.Property(e => e.WorkId).HasColumnName("work_id").IsRequired();
        builder.Property(e => e.Language).HasColumnName("language").HasMaxLength(20).IsRequired();
        builder.Property(e => e.Translator).HasColumnName("translator").HasMaxLength(200);
        builder.Property(e => e.Publisher).HasColumnName("publisher").HasMaxLength(300);
        builder.Property(e => e.EditionYear).HasColumnName("edition_year");
        builder.Property(e => e.Verified).HasColumnName("verified").IsRequired();

        builder.HasIndex(e => e.WorkId);
        builder.Ignore(e => e.DomainEvents);
    }
}

internal sealed class PassageConfiguration : IEntityTypeConfiguration<Passage>
{
    public void Configure(EntityTypeBuilder<Passage> builder)
    {
        builder.ToTable("passages");
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).ValueGeneratedNever();

        builder.Property(p => p.EditionId).HasColumnName("edition_id").IsRequired();
        builder.Property(p => p.ReferencePath).HasColumnName("reference_path").HasMaxLength(500).IsRequired();
        builder.Property(p => p.Text).HasColumnName("text").IsRequired();
        builder.Property(p => p.SortOrder).HasColumnName("sort_order").IsRequired();

        builder.Ignore(p => p.Revision);

        builder.OwnsMany(p => p.Revisions, rev =>
        {
            rev.ToTable("passage_revisions");
            rev.WithOwner().HasForeignKey("passage_id");
            rev.HasKey("Id");
            rev.Property(x => x.Id).ValueGeneratedNever();
            rev.Property(x => x.Revision).HasColumnName("revision").IsRequired();
            rev.Property(x => x.Text).HasColumnName("text").IsRequired();
            rev.Property(x => x.CorrectedOn).HasColumnName("corrected_on").IsRequired();
        });

        builder.HasIndex(p => p.EditionId);
        builder.Ignore(p => p.DomainEvents);
    }
}

internal sealed class CategoryConfiguration : IEntityTypeConfiguration<Category>
{
    public void Configure(EntityTypeBuilder<Category> builder)
    {
        builder.ToTable("categories");
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).ValueGeneratedNever();

        builder.Property(c => c.Name).HasColumnName("name").HasMaxLength(200).IsRequired();
        builder.Property(c => c.Description).HasColumnName("description").HasMaxLength(1000);

        builder.Ignore(c => c.DomainEvents);
    }
}

internal sealed class TopicConfiguration : IEntityTypeConfiguration<Topic>
{
    public void Configure(EntityTypeBuilder<Topic> builder)
    {
        builder.ToTable("topics");
        builder.HasKey(t => t.Id);
        builder.Property(t => t.Id).ValueGeneratedNever();

        builder.Property(t => t.Name).HasColumnName("name").HasMaxLength(200).IsRequired();
        builder.Property(t => t.Description).HasColumnName("description").HasMaxLength(1000);

        builder.Ignore(t => t.DomainEvents);
    }
}

internal sealed class QuestionConfiguration : IEntityTypeConfiguration<Question>
{
    public void Configure(EntityTypeBuilder<Question> builder)
    {
        builder.ToTable("questions");
        builder.HasKey(q => q.Id);
        builder.Property(q => q.Id).ValueGeneratedNever();

        builder.Property(q => q.Title).HasColumnName("title").HasMaxLength(300).IsRequired();
        builder.Property(q => q.Body).HasColumnName("body").IsRequired();
        builder.Property(q => q.AuthorId).HasColumnName("author_id").IsRequired();
        builder.Property(q => q.CategoryId).HasColumnName("category_id");
        builder.Property(q => q.OrganizationUnitId).HasColumnName("organization_unit_id");
        builder.Property(q => q.AcceptedAnswerId).HasColumnName("accepted_answer_id");
        builder.Property(q => q.MergedOntoQuestionId).HasColumnName("merged_onto_question_id");
        builder.Property(q => q.CreatedOn).HasColumnName("created_on").IsRequired();

        builder.Property(q => q.Status)
            .HasColumnName("status")
            .HasConversion(v => v.Id, id => QuestionStatus.FromId(id))
            .IsRequired();

        builder.OwnsMany(q => q.Tags, tag =>
        {
            tag.ToTable("tags");
            tag.WithOwner().HasForeignKey("question_id");
            tag.HasKey("Id");
            tag.Property(x => x.Id).ValueGeneratedNever();
            tag.Property(x => x.Name).HasColumnName("name").HasMaxLength(100).IsRequired();
        });

        builder.OwnsMany(q => q.ModerationFlags, flag =>
        {
            flag.ToTable("moderation_flags");
            flag.WithOwner().HasForeignKey("question_id");
            flag.HasKey("Id");
            flag.Property(x => x.Id).ValueGeneratedNever();
            flag.Property(x => x.Reason).HasColumnName("reason").HasMaxLength(500).IsRequired();
            flag.Property(x => x.FlaggedBy).HasColumnName("flagged_by");
            flag.Property(x => x.FlaggedOn).HasColumnName("flagged_on").IsRequired();
        });

        builder.OwnsMany(q => q.LifecycleEvents, ev =>
        {
            ev.ToTable("question_lifecycle_events");
            ev.WithOwner().HasForeignKey("question_id");
            ev.HasKey("Id");
            ev.Property(x => x.Id).ValueGeneratedNever();
            ev.Property(x => x.FromStatus).HasColumnName("from_status").HasMaxLength(50).IsRequired();
            ev.Property(x => x.ToStatus).HasColumnName("to_status").HasMaxLength(50).IsRequired();
            ev.Property(x => x.ActorId).HasColumnName("actor_id");
            ev.Property(x => x.OccurredOn).HasColumnName("occurred_on").IsRequired();
        });

        builder.HasIndex(q => q.OrganizationUnitId);
        builder.Ignore(q => q.DomainEvents);
    }
}

internal sealed class AnswerConfiguration : IEntityTypeConfiguration<Answer>
{
    public void Configure(EntityTypeBuilder<Answer> builder)
    {
        builder.ToTable("answers");
        builder.HasKey(a => a.Id);
        builder.Property(a => a.Id).ValueGeneratedNever();

        builder.Property(a => a.QuestionId).HasColumnName("question_id").IsRequired();
        builder.Property(a => a.AuthorId).HasColumnName("author_id").IsRequired();
        builder.Property(a => a.Body).HasColumnName("body").IsRequired();
        builder.Property(a => a.ModelId).HasColumnName("model_id").HasMaxLength(200);
        builder.Property(a => a.SuggestionId).HasColumnName("suggestion_id");
        builder.Property(a => a.Accepted).HasColumnName("accepted").IsRequired();

        builder.Ignore(a => a.Revision);

        builder.Property(a => a.Source)
            .HasColumnName("source")
            .HasConversion(v => v.Id, id => AnswerSource.FromId(id))
            .IsRequired();

        builder.OwnsMany(a => a.Revisions, rev =>
        {
            rev.ToTable("answer_revisions");
            rev.WithOwner().HasForeignKey("answer_id");
            rev.HasKey("Id");
            rev.Property(x => x.Id).ValueGeneratedNever();
            rev.Property(x => x.Revision).HasColumnName("revision").IsRequired();
            rev.Property(x => x.Body).HasColumnName("body").IsRequired();
            rev.Property(x => x.RevisedOn).HasColumnName("revised_on").IsRequired();
        });

        builder.HasIndex(a => a.QuestionId);
        builder.Ignore(a => a.DomainEvents);
    }
}

internal sealed class DiscussionConfiguration : IEntityTypeConfiguration<Discussion>
{
    public void Configure(EntityTypeBuilder<Discussion> builder)
    {
        builder.ToTable("discussions");
        builder.HasKey(d => d.Id);
        builder.Property(d => d.Id).ValueGeneratedNever();

        builder.Property(d => d.QuestionId).HasColumnName("question_id").IsRequired();
        builder.Property(d => d.OrganizationUnitId).HasColumnName("organization_unit_id");
        builder.Property(d => d.Title).HasColumnName("title").HasMaxLength(300).IsRequired();
        builder.Property(d => d.AuthorId).HasColumnName("author_id").IsRequired();
        builder.Property(d => d.CreatedOn).HasColumnName("created_on").IsRequired();

        builder.Property(d => d.Status)
            .HasColumnName("status")
            .HasConversion(v => v.Id, id => DiscussionStatus.FromId(id))
            .IsRequired();

        builder.OwnsMany(d => d.Comments, comment =>
        {
            comment.ToTable("comments");
            comment.WithOwner().HasForeignKey("discussion_id");
            comment.HasKey("Id");
            comment.Property(x => x.Id).ValueGeneratedNever();
            comment.Property(x => x.AuthorId).HasColumnName("author_id").IsRequired();
            comment.Property(x => x.Body).HasColumnName("body").IsRequired();
            comment.Property(x => x.CreatedOn).HasColumnName("created_on").IsRequired();
        });

        builder.HasIndex(d => d.QuestionId);
        builder.Ignore(d => d.DomainEvents);
    }
}

internal sealed class ReferenceConfiguration : IEntityTypeConfiguration<Reference>
{
    public void Configure(EntityTypeBuilder<Reference> builder)
    {
        builder.ToTable("references");
        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).ValueGeneratedNever();

        builder.Property(r => r.OwnerType)
            .HasColumnName("owner_type")
            .HasConversion(v => v.Id, id => ReferenceOwnerType.FromId(id))
            .IsRequired();
        builder.Property(r => r.OwnerId).HasColumnName("owner_id").IsRequired();
        builder.Property(r => r.PassageId).HasColumnName("passage_id").IsRequired();
        builder.Property(r => r.EditionId).HasColumnName("edition_id").IsRequired();

        builder.HasIndex(r => new { r.OwnerType, r.OwnerId });
        builder.HasIndex(r => r.PassageId);
        builder.Ignore(r => r.DomainEvents);
    }
}

internal sealed class AiSuggestionConfiguration : IEntityTypeConfiguration<AiSuggestion>
{
    public void Configure(EntityTypeBuilder<AiSuggestion> builder)
    {
        builder.ToTable("ai_suggestions");
        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id).ValueGeneratedNever();

        builder.Property(s => s.QuestionId).HasColumnName("question_id").IsRequired();
        builder.Property(s => s.OrganizationUnitId).HasColumnName("organization_unit_id");
        builder.Property(s => s.Body).HasColumnName("body").IsRequired();
        builder.Property(s => s.ModelId).HasColumnName("model_id").HasMaxLength(200).IsRequired();
        builder.Property(s => s.PromptVersion).HasColumnName("prompt_version").HasMaxLength(100).IsRequired();
        builder.Property(s => s.RequestedOn).HasColumnName("requested_on").IsRequired();
        builder.Property(s => s.ReviewedOn).HasColumnName("reviewed_on");

        builder.Property(s => s.ReviewState)
            .HasColumnName("review_state")
            .HasConversion(v => v.Id, id => AiSuggestionReviewState.FromId(id))
            .IsRequired();

        builder.HasIndex(s => s.QuestionId);
        builder.Ignore(s => s.DomainEvents);
    }
}

internal sealed class OrganizationUnitReferenceConfiguration : IEntityTypeConfiguration<OrganizationUnitReference>
{
    public void Configure(EntityTypeBuilder<OrganizationUnitReference> builder)
    {
        builder.ToTable("organization_unit_references");
        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).ValueGeneratedNever();

        builder.Property(r => r.OrganizationId).HasColumnName("organization_id").IsRequired();
        builder.Property(r => r.Name).HasColumnName("name").HasMaxLength(200).IsRequired();
        builder.Property(r => r.UnitType).HasColumnName("unit_type").HasMaxLength(100).IsRequired();
        builder.Property(r => r.ParentId).HasColumnName("parent_id");
        builder.Property(r => r.LastSeenOn).HasColumnName("last_seen_on").IsRequired();
        builder.Ignore(r => r.DomainEvents);

        builder.HasIndex(r => r.OrganizationId).HasDatabaseName("ix_organization_unit_references_organization_id");
    }
}