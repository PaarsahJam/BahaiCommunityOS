using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace CommunityOS.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreateWorkflow : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "workflow");

            migrationBuilder.CreateTable(
                name: "InboxState",
                schema: "workflow",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    MessageId = table.Column<Guid>(type: "uuid", nullable: false),
                    ConsumerId = table.Column<Guid>(type: "uuid", nullable: false),
                    LockId = table.Column<Guid>(type: "uuid", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "bytea", rowVersion: true, nullable: true),
                    Received = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ReceiveCount = table.Column<int>(type: "integer", nullable: false),
                    ExpirationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Consumed = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Delivered = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastSequenceNumber = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InboxState", x => x.Id);
                    table.UniqueConstraint("AK_InboxState_MessageId_ConsumerId", x => new { x.MessageId, x.ConsumerId });
                });

            migrationBuilder.CreateTable(
                name: "organization_unit_references",
                schema: "workflow",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    organization_unit_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_on = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_organization_unit_references", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "OutboxState",
                schema: "workflow",
                columns: table => new
                {
                    OutboxId = table.Column<Guid>(type: "uuid", nullable: false),
                    LockId = table.Column<Guid>(type: "uuid", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "bytea", rowVersion: true, nullable: true),
                    Created = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Delivered = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastSequenceNumber = table.Column<long>(type: "bigint", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OutboxState", x => x.OutboxId);
                });

            migrationBuilder.CreateTable(
                name: "task_definitions",
                schema: "workflow",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    display_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    domain_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    due_in = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    requires_human_review = table.Column<bool>(type: "boolean", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    created_on = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_on = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    retired_by = table.Column<Guid>(type: "uuid", nullable: true),
                    retired_on = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_task_definitions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "workflow_tasks",
                schema: "workflow",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    definition_code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    domain_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    domain_entity_id = table.Column<Guid>(type: "uuid", nullable: true),
                    organization_unit_id = table.Column<Guid>(type: "uuid", nullable: true),
                    status = table.Column<int>(type: "integer", nullable: false),
                    notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    due_on = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    originator_id = table.Column<Guid>(type: "uuid", nullable: false),
                    escalated_to = table.Column<Guid>(type: "uuid", nullable: true),
                    escalated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    escalated_on = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    escalation_reason = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    outcome = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    created_on = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_on = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    started_by = table.Column<Guid>(type: "uuid", nullable: true),
                    started_on = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    completed_by = table.Column<Guid>(type: "uuid", nullable: true),
                    completed_on = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_workflow_tasks", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "OutboxMessage",
                schema: "workflow",
                columns: table => new
                {
                    SequenceNumber = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    EnqueueTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    SentTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Headers = table.Column<string>(type: "text", nullable: true),
                    Properties = table.Column<string>(type: "text", nullable: true),
                    InboxMessageId = table.Column<Guid>(type: "uuid", nullable: true),
                    InboxConsumerId = table.Column<Guid>(type: "uuid", nullable: true),
                    OutboxId = table.Column<Guid>(type: "uuid", nullable: true),
                    MessageId = table.Column<Guid>(type: "uuid", nullable: false),
                    ContentType = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    MessageType = table.Column<string>(type: "text", nullable: false),
                    Body = table.Column<string>(type: "text", nullable: false),
                    ConversationId = table.Column<Guid>(type: "uuid", nullable: true),
                    CorrelationId = table.Column<Guid>(type: "uuid", nullable: true),
                    InitiatorId = table.Column<Guid>(type: "uuid", nullable: true),
                    RequestId = table.Column<Guid>(type: "uuid", nullable: true),
                    SourceAddress = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    DestinationAddress = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    ResponseAddress = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    FaultAddress = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    ExpirationTime = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OutboxMessage", x => x.SequenceNumber);
                    table.ForeignKey(
                        name: "FK_OutboxMessage_InboxState_InboxMessageId_InboxConsumerId",
                        columns: x => new { x.InboxMessageId, x.InboxConsumerId },
                        principalSchema: "workflow",
                        principalTable: "InboxState",
                        principalColumns: new[] { "MessageId", "ConsumerId" });
                    table.ForeignKey(
                        name: "FK_OutboxMessage_OutboxState_OutboxId",
                        column: x => x.OutboxId,
                        principalSchema: "workflow",
                        principalTable: "OutboxState",
                        principalColumn: "OutboxId");
                });

            migrationBuilder.CreateTable(
                name: "task_definition_outcomes",
                schema: "workflow",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    task_definition_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_task_definition_outcomes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_task_definition_outcomes_task_definitions_task_definition_id",
                        column: x => x.task_definition_id,
                        principalSchema: "workflow",
                        principalTable: "task_definitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "task_activity",
                schema: "workflow",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    action = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    actor_id = table.Column<Guid>(type: "uuid", nullable: false),
                    occurred_on = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    outcome = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    notes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    task_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_task_activity", x => x.Id);
                    table.ForeignKey(
                        name: "FK_task_activity_workflow_tasks_task_id",
                        column: x => x.task_id,
                        principalSchema: "workflow",
                        principalTable: "workflow_tasks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "task_assignments",
                schema: "workflow",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    assigned_by = table.Column<Guid>(type: "uuid", nullable: false),
                    assigned_on = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    task_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_task_assignments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_task_assignments_workflow_tasks_task_id",
                        column: x => x.task_id,
                        principalSchema: "workflow",
                        principalTable: "workflow_tasks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "task_scopes",
                schema: "workflow",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    organization_unit_id = table.Column<Guid>(type: "uuid", nullable: false),
                    task_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_task_scopes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_task_scopes_workflow_tasks_task_id",
                        column: x => x.task_id,
                        principalSchema: "workflow",
                        principalTable: "workflow_tasks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "task_assignment_assignees",
                schema: "workflow",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    assignee_id = table.Column<Guid>(type: "uuid", nullable: false),
                    task_assignment_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_task_assignment_assignees", x => x.Id);
                    table.ForeignKey(
                        name: "FK_task_assignment_assignees_task_assignments_task_assignment_~",
                        column: x => x.task_assignment_id,
                        principalSchema: "workflow",
                        principalTable: "task_assignments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_InboxState_Delivered",
                schema: "workflow",
                table: "InboxState",
                column: "Delivered");

            migrationBuilder.CreateIndex(
                name: "ix_workflow_organization_unit_references_unit_id",
                schema: "workflow",
                table: "organization_unit_references",
                column: "organization_unit_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OutboxMessage_EnqueueTime",
                schema: "workflow",
                table: "OutboxMessage",
                column: "EnqueueTime");

            migrationBuilder.CreateIndex(
                name: "IX_OutboxMessage_ExpirationTime",
                schema: "workflow",
                table: "OutboxMessage",
                column: "ExpirationTime");

            migrationBuilder.CreateIndex(
                name: "IX_OutboxMessage_InboxMessageId_InboxConsumerId_SequenceNumber",
                schema: "workflow",
                table: "OutboxMessage",
                columns: new[] { "InboxMessageId", "InboxConsumerId", "SequenceNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OutboxMessage_OutboxId_SequenceNumber",
                schema: "workflow",
                table: "OutboxMessage",
                columns: new[] { "OutboxId", "SequenceNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OutboxState_Created",
                schema: "workflow",
                table: "OutboxState",
                column: "Created");

            migrationBuilder.CreateIndex(
                name: "ix_task_activity_task_occurred_on",
                schema: "workflow",
                table: "task_activity",
                columns: new[] { "task_id", "occurred_on" });

            migrationBuilder.CreateIndex(
                name: "IX_task_assignment_assignees_task_assignment_id",
                schema: "workflow",
                table: "task_assignment_assignees",
                column: "task_assignment_id");

            migrationBuilder.CreateIndex(
                name: "IX_task_assignments_task_id",
                schema: "workflow",
                table: "task_assignments",
                column: "task_id");

            migrationBuilder.CreateIndex(
                name: "ix_task_definition_outcomes_definition_code",
                schema: "workflow",
                table: "task_definition_outcomes",
                columns: new[] { "task_definition_id", "code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_task_definitions_code",
                schema: "workflow",
                table: "task_definitions",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_task_scopes_task_organization_unit",
                schema: "workflow",
                table: "task_scopes",
                columns: new[] { "task_id", "organization_unit_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_workflow_tasks_definition_code",
                schema: "workflow",
                table: "workflow_tasks",
                column: "definition_code");

            migrationBuilder.CreateIndex(
                name: "ix_workflow_tasks_domain_entity_id",
                schema: "workflow",
                table: "workflow_tasks",
                column: "domain_entity_id");

            migrationBuilder.CreateIndex(
                name: "ix_workflow_tasks_open_definition_domain",
                schema: "workflow",
                table: "workflow_tasks",
                columns: new[] { "definition_code", "domain_type", "domain_entity_id" },
                unique: true,
                filter: "\"status\" < 4 AND \"domain_entity_id\" IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_workflow_tasks_organization_unit_id",
                schema: "workflow",
                table: "workflow_tasks",
                column: "organization_unit_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "organization_unit_references",
                schema: "workflow");

            migrationBuilder.DropTable(
                name: "OutboxMessage",
                schema: "workflow");

            migrationBuilder.DropTable(
                name: "task_activity",
                schema: "workflow");

            migrationBuilder.DropTable(
                name: "task_assignment_assignees",
                schema: "workflow");

            migrationBuilder.DropTable(
                name: "task_definition_outcomes",
                schema: "workflow");

            migrationBuilder.DropTable(
                name: "task_scopes",
                schema: "workflow");

            migrationBuilder.DropTable(
                name: "InboxState",
                schema: "workflow");

            migrationBuilder.DropTable(
                name: "OutboxState",
                schema: "workflow");

            migrationBuilder.DropTable(
                name: "task_assignments",
                schema: "workflow");

            migrationBuilder.DropTable(
                name: "task_definitions",
                schema: "workflow");

            migrationBuilder.DropTable(
                name: "workflow_tasks",
                schema: "workflow");
        }
    }
}
