using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace CommunityOS.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreateRecords : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "records");

            migrationBuilder.CreateTable(
                name: "InboxState",
                schema: "records",
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
                schema: "records",
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
                schema: "records",
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
                name: "record_categories",
                schema: "records",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    display_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    is_retired = table.Column<bool>(type: "boolean", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    created_on = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_on = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_record_categories", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "records",
                schema: "records",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    category = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    status = table.Column<int>(type: "integer", nullable: false),
                    subject_type = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    subject_id = table.Column<Guid>(type: "uuid", nullable: false),
                    organization_unit_id = table.Column<Guid>(type: "uuid", nullable: true),
                    current_version_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    created_on = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_on = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    verified_by = table.Column<Guid>(type: "uuid", nullable: true),
                    verified_on = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_records", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "retention_schedules",
                schema: "records",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    display_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    is_retired = table.Column<bool>(type: "boolean", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    created_on = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: true),
                    updated_on = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_retention_schedules", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "OutboxMessage",
                schema: "records",
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
                        principalSchema: "records",
                        principalTable: "InboxState",
                        principalColumns: new[] { "MessageId", "ConsumerId" });
                    table.ForeignKey(
                        name: "FK_OutboxMessage_OutboxState_OutboxId",
                        column: x => x.OutboxId,
                        principalSchema: "records",
                        principalTable: "OutboxState",
                        principalColumn: "OutboxId");
                });

            migrationBuilder.CreateTable(
                name: "record_classification",
                schema: "records",
                columns: table => new
                {
                    record_id = table.Column<Guid>(type: "uuid", nullable: false),
                    classification_code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    is_sensitive = table.Column<bool>(type: "boolean", nullable: false),
                    retention_schedule_code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    retention_expired_on = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    classified_by = table.Column<Guid>(type: "uuid", nullable: true),
                    classified_on = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_record_classification", x => x.record_id);
                    table.ForeignKey(
                        name: "FK_record_classification_records_record_id",
                        column: x => x.record_id,
                        principalSchema: "records",
                        principalTable: "records",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "record_evidence_references",
                schema: "records",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    document_id = table.Column<Guid>(type: "uuid", nullable: false),
                    version_number = table.Column<int>(type: "integer", nullable: false),
                    reference_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    attached_by = table.Column<Guid>(type: "uuid", nullable: false),
                    attached_on = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    document_deactivated_on = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    document_restored_on = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    record_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_record_evidence_references", x => x.Id);
                    table.ForeignKey(
                        name: "FK_record_evidence_references_records_record_id",
                        column: x => x.record_id,
                        principalSchema: "records",
                        principalTable: "records",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "record_holds",
                schema: "records",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    hold_type = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    reason = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    placed_by = table.Column<Guid>(type: "uuid", nullable: false),
                    placed_on = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    released_by = table.Column<Guid>(type: "uuid", nullable: true),
                    released_on = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    record_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_record_holds", x => x.Id);
                    table.ForeignKey(
                        name: "FK_record_holds_records_record_id",
                        column: x => x.record_id,
                        principalSchema: "records",
                        principalTable: "records",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "record_scopes",
                schema: "records",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    organization_unit_id = table.Column<Guid>(type: "uuid", nullable: false),
                    record_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_record_scopes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_record_scopes_records_record_id",
                        column: x => x.record_id,
                        principalSchema: "records",
                        principalTable: "records",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "record_versions",
                schema: "records",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    version_number = table.Column<int>(type: "integer", nullable: false),
                    supersedes_version_number = table.Column<int>(type: "integer", nullable: true),
                    applied_by = table.Column<Guid>(type: "uuid", nullable: false),
                    applied_on = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    change_reason = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    record_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_record_versions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_record_versions_records_record_id",
                        column: x => x.record_id,
                        principalSchema: "records",
                        principalTable: "records",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "record_working_fields",
                schema: "records",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    field_key = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    field_value = table.Column<string>(type: "character varying(8000)", maxLength: 8000, nullable: false),
                    is_sensitive = table.Column<bool>(type: "boolean", nullable: false),
                    record_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_record_working_fields", x => x.Id);
                    table.ForeignKey(
                        name: "FK_record_working_fields_records_record_id",
                        column: x => x.record_id,
                        principalSchema: "records",
                        principalTable: "records",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "retention_rules",
                schema: "records",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    category = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    start_trigger = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    period = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    disposition = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    note = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    retention_schedule_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_retention_rules", x => x.Id);
                    table.ForeignKey(
                        name: "FK_retention_rules_retention_schedules_retention_schedule_id",
                        column: x => x.retention_schedule_id,
                        principalSchema: "records",
                        principalTable: "retention_schedules",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "record_hold_document_references",
                schema: "records",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    document_id = table.Column<Guid>(type: "uuid", nullable: false),
                    version_number = table.Column<int>(type: "integer", nullable: true),
                    record_hold_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_record_hold_document_references", x => x.Id);
                    table.ForeignKey(
                        name: "FK_record_hold_document_references_record_holds_record_hold_id",
                        column: x => x.record_hold_id,
                        principalSchema: "records",
                        principalTable: "record_holds",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "record_field_values",
                schema: "records",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    field_key = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    field_value = table.Column<string>(type: "character varying(8000)", maxLength: 8000, nullable: false),
                    is_sensitive = table.Column<bool>(type: "boolean", nullable: false),
                    record_version_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_record_field_values", x => x.Id);
                    table.ForeignKey(
                        name: "FK_record_field_values_record_versions_record_version_id",
                        column: x => x.record_version_id,
                        principalSchema: "records",
                        principalTable: "record_versions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_InboxState_Delivered",
                schema: "records",
                table: "InboxState",
                column: "Delivered");

            migrationBuilder.CreateIndex(
                name: "ix_records_organization_unit_references_unit_id",
                schema: "records",
                table: "organization_unit_references",
                column: "organization_unit_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OutboxMessage_EnqueueTime",
                schema: "records",
                table: "OutboxMessage",
                column: "EnqueueTime");

            migrationBuilder.CreateIndex(
                name: "IX_OutboxMessage_ExpirationTime",
                schema: "records",
                table: "OutboxMessage",
                column: "ExpirationTime");

            migrationBuilder.CreateIndex(
                name: "IX_OutboxMessage_InboxMessageId_InboxConsumerId_SequenceNumber",
                schema: "records",
                table: "OutboxMessage",
                columns: new[] { "InboxMessageId", "InboxConsumerId", "SequenceNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OutboxMessage_OutboxId_SequenceNumber",
                schema: "records",
                table: "OutboxMessage",
                columns: new[] { "OutboxId", "SequenceNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OutboxState_Created",
                schema: "records",
                table: "OutboxState",
                column: "Created");

            migrationBuilder.CreateIndex(
                name: "ix_record_categories_code",
                schema: "records",
                table: "record_categories",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_record_evidence_document_version_type",
                schema: "records",
                table: "record_evidence_references",
                columns: new[] { "record_id", "document_id", "version_number", "reference_type" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_record_field_values_record_version_id",
                schema: "records",
                table: "record_field_values",
                column: "record_version_id");

            migrationBuilder.CreateIndex(
                name: "IX_record_hold_document_references_record_hold_id",
                schema: "records",
                table: "record_hold_document_references",
                column: "record_hold_id");

            migrationBuilder.CreateIndex(
                name: "IX_record_holds_record_id",
                schema: "records",
                table: "record_holds",
                column: "record_id");

            migrationBuilder.CreateIndex(
                name: "ix_record_scopes_record_organization_unit",
                schema: "records",
                table: "record_scopes",
                columns: new[] { "record_id", "organization_unit_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_record_versions_record_version_number",
                schema: "records",
                table: "record_versions",
                columns: new[] { "record_id", "version_number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_record_working_fields_record_id",
                schema: "records",
                table: "record_working_fields",
                column: "record_id");

            migrationBuilder.CreateIndex(
                name: "ix_records_current_version_id",
                schema: "records",
                table: "records",
                column: "current_version_id");

            migrationBuilder.CreateIndex(
                name: "ix_records_organization_unit_id",
                schema: "records",
                table: "records",
                column: "organization_unit_id");

            migrationBuilder.CreateIndex(
                name: "IX_retention_rules_retention_schedule_id",
                schema: "records",
                table: "retention_rules",
                column: "retention_schedule_id");

            migrationBuilder.CreateIndex(
                name: "ix_retention_schedules_code",
                schema: "records",
                table: "retention_schedules",
                column: "code",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "organization_unit_references",
                schema: "records");

            migrationBuilder.DropTable(
                name: "OutboxMessage",
                schema: "records");

            migrationBuilder.DropTable(
                name: "record_categories",
                schema: "records");

            migrationBuilder.DropTable(
                name: "record_classification",
                schema: "records");

            migrationBuilder.DropTable(
                name: "record_evidence_references",
                schema: "records");

            migrationBuilder.DropTable(
                name: "record_field_values",
                schema: "records");

            migrationBuilder.DropTable(
                name: "record_hold_document_references",
                schema: "records");

            migrationBuilder.DropTable(
                name: "record_scopes",
                schema: "records");

            migrationBuilder.DropTable(
                name: "record_working_fields",
                schema: "records");

            migrationBuilder.DropTable(
                name: "retention_rules",
                schema: "records");

            migrationBuilder.DropTable(
                name: "InboxState",
                schema: "records");

            migrationBuilder.DropTable(
                name: "OutboxState",
                schema: "records");

            migrationBuilder.DropTable(
                name: "record_versions",
                schema: "records");

            migrationBuilder.DropTable(
                name: "record_holds",
                schema: "records");

            migrationBuilder.DropTable(
                name: "retention_schedules",
                schema: "records");

            migrationBuilder.DropTable(
                name: "records",
                schema: "records");
        }
    }
}
