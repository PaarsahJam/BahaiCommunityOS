using CommunityOS.Workflow.Domain.Aggregates;
using CommunityOS.Workflow.Domain.Enumerations;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace CommunityOS.Workflow.Infrastructure.Persistence.Configurations;

internal sealed class WorkflowTaskConfiguration : IEntityTypeConfiguration<WorkflowTask>
{
    public void Configure(EntityTypeBuilder<WorkflowTask> builder)
    {
        builder.ToTable("workflow_tasks");
        builder.HasKey(t => t.Id);
        builder.Property(t => t.Id).ValueGeneratedNever();

        builder.Property(t => t.DefinitionCode).HasColumnName("definition_code").HasMaxLength(100).IsRequired();
        builder.Property(t => t.DomainType).HasColumnName("domain_type").HasMaxLength(50).IsRequired();
        builder.Property(t => t.DomainEntityId).HasColumnName("domain_entity_id");
        builder.Property(t => t.OrganizationUnitId).HasColumnName("organization_unit_id");
        builder.Property(t => t.Notes).HasColumnName("notes").HasMaxLength(2000);
        builder.Property(t => t.DueOn).HasColumnName("due_on");
        builder.Property(t => t.OriginatorId).HasColumnName("originator_id").IsRequired();
        builder.Property(t => t.EscalatedTo).HasColumnName("escalated_to");
        builder.Property(t => t.EscalatedBy).HasColumnName("escalated_by");
        builder.Property(t => t.EscalatedOn).HasColumnName("escalated_on");
        builder.Property(t => t.EscalationReason).HasColumnName("escalation_reason").HasMaxLength(2000);
        builder.Property(t => t.Outcome).HasColumnName("outcome").HasMaxLength(50);
        builder.Property(t => t.CreatedBy).HasColumnName("created_by").IsRequired();
        builder.Property(t => t.CreatedOn).HasColumnName("created_on").IsRequired();
        builder.Property(t => t.UpdatedBy).HasColumnName("updated_by").IsRequired();
        builder.Property(t => t.UpdatedOn).HasColumnName("updated_on").IsRequired();
        builder.Property(t => t.StartedBy).HasColumnName("started_by");
        builder.Property(t => t.StartedOn).HasColumnName("started_on");
        builder.Property(t => t.CompletedBy).HasColumnName("completed_by");
        builder.Property(t => t.CompletedOn).HasColumnName("completed_on");

        builder.Property(t => t.Status)
            .HasColumnName("status")
            .HasConversion(v => v.Id, id => WorkflowStatus.FromId(id))
            .IsRequired();

        builder.OwnsMany(t => t.Assignments, a =>
        {
            a.ToTable("task_assignments");
            a.WithOwner().HasForeignKey("task_id");
            a.HasKey("Id");
            a.Property(x => x.Id).ValueGeneratedNever();
            a.Property(x => x.AssignedBy).HasColumnName("assigned_by").IsRequired();
            a.Property(x => x.AssignedOn).HasColumnName("assigned_on").IsRequired();

            a.OwnsMany(x => x.Assignees, ids =>
            {
                ids.ToTable("task_assignment_assignees");
                ids.WithOwner().HasForeignKey("task_assignment_id");
                ids.HasKey(x => x.Id);
                ids.Property(x => x.Id).ValueGeneratedNever();
                ids.Property(x => x.AssigneeId).HasColumnName("assignee_id").IsRequired();
            });
        });

        builder.OwnsMany(t => t.Scopes, s =>
        {
            s.ToTable("task_scopes");
            s.WithOwner().HasForeignKey("task_id");
            s.HasKey("Id");
            s.Property(x => x.Id).ValueGeneratedNever();
            s.Property(x => x.OrganizationUnitId).HasColumnName("organization_unit_id").IsRequired();

            s.HasIndex("task_id", "OrganizationUnitId")
                .IsUnique()
                .HasDatabaseName("ix_task_scopes_task_organization_unit");
        });

        builder.OwnsMany(t => t.Activity, a =>
        {
            a.ToTable("task_activity");
            a.WithOwner().HasForeignKey("task_id");
            a.HasKey("Id");
            a.Property(x => x.Id).ValueGeneratedNever();
            a.Property(x => x.Action).HasColumnName("action").HasMaxLength(50).IsRequired();
            a.Property(x => x.ActorId).HasColumnName("actor_id").IsRequired();
            a.Property(x => x.OccurredOn).HasColumnName("occurred_on").IsRequired();
            a.Property(x => x.Outcome).HasColumnName("outcome").HasMaxLength(50);
            a.Property(x => x.Notes).HasColumnName("notes").HasMaxLength(2000);

            a.HasIndex("task_id", "OccurredOn")
                .HasDatabaseName("ix_task_activity_task_occurred_on");
        });

        builder.HasIndex(t => t.OrganizationUnitId)
            .HasDatabaseName("ix_workflow_tasks_organization_unit_id");
        builder.HasIndex(t => t.DefinitionCode)
            .HasDatabaseName("ix_workflow_tasks_definition_code");
        builder.HasIndex(t => t.DomainEntityId)
            .HasDatabaseName("ix_workflow_tasks_domain_entity_id");

        // Idempotency: one open task per (definition, domain type, domain
        // entity). Filtered to non-terminal tasks and domain-bound rows
        // (domain_entity_id IS NOT NULL); free-standing general tasks are
        // exempt. The reconcile consumers rely on this for
        // create-if-absent/close-if-open reconciliation (ADR-024).
        builder.HasIndex(t => new { t.DefinitionCode, t.DomainType, t.DomainEntityId })
            .IsUnique()
            .HasFilter("\"status\" < 4 AND \"domain_entity_id\" IS NOT NULL")
            .HasDatabaseName("ix_workflow_tasks_open_definition_domain");

        builder.Ignore(t => t.DomainEvents);
    }
}

internal sealed class TaskDefinitionConfiguration : IEntityTypeConfiguration<TaskDefinition>
{
    public void Configure(EntityTypeBuilder<TaskDefinition> builder)
    {
        builder.ToTable("task_definitions");
        builder.HasKey(d => d.Id);
        builder.Property(d => d.Id).ValueGeneratedNever();

        builder.Property(d => d.Code).HasColumnName("code").HasMaxLength(100).IsRequired();
        builder.Property(d => d.DisplayName).HasColumnName("display_name").HasMaxLength(200).IsRequired();
        builder.Property(d => d.Description).HasColumnName("description").HasMaxLength(500);
        builder.Property(d => d.DomainType).HasColumnName("domain_type").HasMaxLength(50).IsRequired();
        builder.Property(d => d.DueIn).HasColumnName("due_in").HasMaxLength(30);
        builder.Property(d => d.RequiresHumanReview).HasColumnName("requires_human_review").IsRequired();
        builder.Property(d => d.IsActive).HasColumnName("is_active").IsRequired();
        builder.Property(d => d.CreatedBy).HasColumnName("created_by").IsRequired();
        builder.Property(d => d.CreatedOn).HasColumnName("created_on").IsRequired();
        builder.Property(d => d.UpdatedBy).HasColumnName("updated_by").IsRequired();
        builder.Property(d => d.UpdatedOn).HasColumnName("updated_on").IsRequired();
        builder.Property(d => d.RetiredBy).HasColumnName("retired_by");
        builder.Property(d => d.RetiredOn).HasColumnName("retired_on");

        builder.OwnsMany(d => d.PermittedOutcomes, o =>
        {
            o.ToTable("task_definition_outcomes");
            o.WithOwner().HasForeignKey("task_definition_id");
            o.HasKey("Id");
            o.Property(x => x.Id).ValueGeneratedNever();
            o.Property(x => x.Code).HasColumnName("code").HasMaxLength(50).IsRequired();

            o.HasIndex("task_definition_id", "Code")
                .IsUnique()
                .HasDatabaseName("ix_task_definition_outcomes_definition_code");
        });

        builder.HasIndex(d => d.Code).IsUnique().HasDatabaseName("ix_task_definitions_code");
        builder.Ignore(d => d.DomainEvents);
    }
}

internal sealed class WorkflowOrganizationUnitReferenceConfiguration
    : IEntityTypeConfiguration<OrganizationUnitReference>
{
    public void Configure(EntityTypeBuilder<OrganizationUnitReference> builder)
    {
        builder.ToTable("organization_unit_references");
        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).ValueGeneratedNever();

        builder.Property(r => r.OrganizationUnitId).HasColumnName("organization_unit_id").IsRequired();
        builder.Property(r => r.CreatedOn).HasColumnName("created_on").IsRequired();
        builder.Ignore(r => r.DomainEvents);

        builder.HasIndex(r => r.OrganizationUnitId).IsUnique()
            .HasDatabaseName("ix_workflow_organization_unit_references_unit_id");
    }
}