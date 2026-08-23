using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace CommunityOS.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreateCorrespondence : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "correspondence");

            migrationBuilder.CreateTable(
                name: "export_activity",
                schema: "correspondence",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    requested_by = table.Column<Guid>(type: "uuid", nullable: false),
                    format = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    filter_summary = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    row_count = table.Column<int>(type: "integer", nullable: false),
                    included_sensitive = table.Column<bool>(type: "boolean", nullable: false),
                    requested_on = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_export_activity", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "InboxState",
                schema: "correspondence",
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
                name: "letter_status_history",
                schema: "correspondence",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    letter_id = table.Column<Guid>(type: "uuid", nullable: true),
                    from_status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    to_status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    cause = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    actor_id = table.Column<Guid>(type: "uuid", nullable: true),
                    reason_code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    occurred_on = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_letter_status_history", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "letters",
                schema: "correspondence",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    organization_unit_id = table.Column<Guid>(type: "uuid", nullable: false),
                    category_code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    subject = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    body = table.Column<string>(type: "text", nullable: false),
                    sensitivity = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    revision = table.Column<int>(type: "integer", nullable: false),
                    letter_year = table.Column<int>(type: "integer", nullable: true),
                    letter_sequence = table.Column<int>(type: "integer", nullable: true),
                    template_id = table.Column<Guid>(type: "uuid", nullable: true),
                    template_code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    related_letter_id = table.Column<Guid>(type: "uuid", nullable: true),
                    subject_person_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    created_on = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_on = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    submitted_by = table.Column<Guid>(type: "uuid", nullable: true),
                    submitted_on = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    materialized_on = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    dispatched_on = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    delivered_on = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    delivery_failure_reason_code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    cancelled_by = table.Column<Guid>(type: "uuid", nullable: true),
                    cancelled_on = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    cancellation_reason_code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    retention_class = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    retention_expires_on = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastDispatchMethodCode = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_letters", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "organization_unit_references",
                schema: "correspondence",
                columns: table => new
                {
                    organization_unit_id = table.Column<Guid>(type: "uuid", nullable: false),
                    parent_organization_unit_id = table.Column<Guid>(type: "uuid", nullable: true),
                    last_updated_on = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_organization_unit_references", x => x.organization_unit_id);
                });

            migrationBuilder.CreateTable(
                name: "OutboxState",
                schema: "correspondence",
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
                name: "templates",
                schema: "correspondence",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    subject_template = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    body_template = table.Column<string>(type: "text", nullable: false),
                    category_code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    created_on = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_on = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_templates", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "letter_attachments",
                schema: "correspondence",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    letter_id = table.Column<Guid>(type: "uuid", nullable: false),
                    document_id = table.Column<Guid>(type: "uuid", nullable: false),
                    reference_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    added_by = table.Column<Guid>(type: "uuid", nullable: false),
                    added_on = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_letter_attachments", x => x.id);
                    table.ForeignKey(
                        name: "FK_letter_attachments_letters_letter_id",
                        column: x => x.letter_id,
                        principalSchema: "correspondence",
                        principalTable: "letters",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "letter_delivery_records",
                schema: "correspondence",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    letter_id = table.Column<Guid>(type: "uuid", nullable: false),
                    method_code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    outcome = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    reason_code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    actor_id = table.Column<Guid>(type: "uuid", nullable: false),
                    occurred_on = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_letter_delivery_records", x => x.id);
                    table.ForeignKey(
                        name: "FK_letter_delivery_records_letters_letter_id",
                        column: x => x.letter_id,
                        principalSchema: "correspondence",
                        principalTable: "letters",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "letter_documents",
                schema: "correspondence",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    letter_id = table.Column<Guid>(type: "uuid", nullable: false),
                    document_id = table.Column<Guid>(type: "uuid", nullable: false),
                    version_number = table.Column<int>(type: "integer", nullable: false),
                    content_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    materialized_on = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_letter_documents", x => x.id);
                    table.ForeignKey(
                        name: "FK_letter_documents_letters_letter_id",
                        column: x => x.letter_id,
                        principalSchema: "correspondence",
                        principalTable: "letters",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "letter_holds",
                schema: "correspondence",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    letter_id = table.Column<Guid>(type: "uuid", nullable: false),
                    hold_type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    reason_code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    placed_by = table.Column<Guid>(type: "uuid", nullable: false),
                    placed_on = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    released_by = table.Column<Guid>(type: "uuid", nullable: true),
                    released_on = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_letter_holds", x => x.id);
                    table.ForeignKey(
                        name: "FK_letter_holds_letters_letter_id",
                        column: x => x.letter_id,
                        principalSchema: "correspondence",
                        principalTable: "letters",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "letter_recipients",
                schema: "correspondence",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    letter_id = table.Column<Guid>(type: "uuid", nullable: false),
                    kind = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    person_id = table.Column<Guid>(type: "uuid", nullable: true),
                    unit_id = table.Column<Guid>(type: "uuid", nullable: true),
                    display_line = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    added_on = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_letter_recipients", x => x.id);
                    table.ForeignKey(
                        name: "FK_letter_recipients_letters_letter_id",
                        column: x => x.letter_id,
                        principalSchema: "correspondence",
                        principalTable: "letters",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "OutboxMessage",
                schema: "correspondence",
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
                        principalSchema: "correspondence",
                        principalTable: "InboxState",
                        principalColumns: new[] { "MessageId", "ConsumerId" });
                    table.ForeignKey(
                        name: "FK_OutboxMessage_OutboxState_OutboxId",
                        column: x => x.OutboxId,
                        principalSchema: "correspondence",
                        principalTable: "OutboxState",
                        principalColumn: "OutboxId");
                });

            migrationBuilder.CreateIndex(
                name: "ix_export_activity_requested_on",
                schema: "correspondence",
                table: "export_activity",
                column: "requested_on");

            migrationBuilder.CreateIndex(
                name: "IX_InboxState_Delivered",
                schema: "correspondence",
                table: "InboxState",
                column: "Delivered");

            migrationBuilder.CreateIndex(
                name: "ux_letter_attachments_letter_document",
                schema: "correspondence",
                table: "letter_attachments",
                columns: new[] { "letter_id", "document_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_letter_delivery_records_letter_id",
                schema: "correspondence",
                table: "letter_delivery_records",
                column: "letter_id");

            migrationBuilder.CreateIndex(
                name: "IX_letter_documents_letter_id",
                schema: "correspondence",
                table: "letter_documents",
                column: "letter_id");

            migrationBuilder.CreateIndex(
                name: "ux_letter_documents_document_id",
                schema: "correspondence",
                table: "letter_documents",
                column: "document_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_letter_holds_active",
                schema: "correspondence",
                table: "letter_holds",
                column: "letter_id",
                filter: "released_on IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_letter_recipients_letter_id",
                schema: "correspondence",
                table: "letter_recipients",
                column: "letter_id");

            migrationBuilder.CreateIndex(
                name: "ix_letter_recipients_person_id",
                schema: "correspondence",
                table: "letter_recipients",
                column: "person_id");

            migrationBuilder.CreateIndex(
                name: "ix_letter_recipients_unit_id",
                schema: "correspondence",
                table: "letter_recipients",
                column: "unit_id");

            migrationBuilder.CreateIndex(
                name: "ix_letter_status_history_letter_id",
                schema: "correspondence",
                table: "letter_status_history",
                column: "letter_id");

            migrationBuilder.CreateIndex(
                name: "ix_letter_status_history_occurred_on",
                schema: "correspondence",
                table: "letter_status_history",
                column: "occurred_on");

            migrationBuilder.CreateIndex(
                name: "ix_letters_created_on",
                schema: "correspondence",
                table: "letters",
                column: "created_on");

            migrationBuilder.CreateIndex(
                name: "ix_letters_organization_unit_id",
                schema: "correspondence",
                table: "letters",
                column: "organization_unit_id");

            migrationBuilder.CreateIndex(
                name: "ix_letters_retention_expiry",
                schema: "correspondence",
                table: "letters",
                columns: new[] { "retention_class", "retention_expires_on" },
                filter: "retention_expires_on IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_letters_status_submitted_on",
                schema: "correspondence",
                table: "letters",
                columns: new[] { "status", "submitted_on" });

            migrationBuilder.CreateIndex(
                name: "ux_letters_unit_year_sequence",
                schema: "correspondence",
                table: "letters",
                columns: new[] { "organization_unit_id", "letter_year", "letter_sequence" },
                unique: true,
                filter: "letter_year IS NOT NULL AND letter_sequence IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_OutboxMessage_EnqueueTime",
                schema: "correspondence",
                table: "OutboxMessage",
                column: "EnqueueTime");

            migrationBuilder.CreateIndex(
                name: "IX_OutboxMessage_ExpirationTime",
                schema: "correspondence",
                table: "OutboxMessage",
                column: "ExpirationTime");

            migrationBuilder.CreateIndex(
                name: "IX_OutboxMessage_InboxMessageId_InboxConsumerId_SequenceNumber",
                schema: "correspondence",
                table: "OutboxMessage",
                columns: new[] { "InboxMessageId", "InboxConsumerId", "SequenceNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OutboxMessage_OutboxId_SequenceNumber",
                schema: "correspondence",
                table: "OutboxMessage",
                columns: new[] { "OutboxId", "SequenceNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OutboxState_Created",
                schema: "correspondence",
                table: "OutboxState",
                column: "Created");

            migrationBuilder.CreateIndex(
                name: "ux_templates_code",
                schema: "correspondence",
                table: "templates",
                column: "code",
                unique: true);

            // Purge guard (ADR-028 decision 13, Audit pattern): retention
            // purge is the ONLY removal path for letters, and the lifecycle
            // history trail is append-only. Native triggers reject unguarded
            // DELETEs on letters and any UPDATE/DELETE on history unless the
            // purge transaction sets app.correspondence_purge_authorized='on'
            // via SET LOCAL. Ordinary letter lifecycle UPDATEs are legitimate
            // aggregate mutations (optimistic revision) and stay permitted.
            migrationBuilder.Sql("""
                CREATE FUNCTION correspondence.reject_unauthorized_mutation() RETURNS trigger AS $$
                BEGIN
                    IF current_setting('app.correspondence_purge_authorized', true) = 'on' THEN
                        RETURN COALESCE(NEW, OLD);
                    END IF;
                    RAISE EXCEPTION 'Mutations require app.correspondence_purge_authorized=on (purge path only)';
                END;
                $$ LANGUAGE plpgsql;

                CREATE TRIGGER trg_letters_no_delete
                    BEFORE DELETE ON correspondence.letters
                    FOR EACH ROW EXECUTE FUNCTION correspondence.reject_unauthorized_mutation();

                CREATE TRIGGER trg_letter_status_history_no_update
                    BEFORE UPDATE ON correspondence.letter_status_history
                    FOR EACH ROW EXECUTE FUNCTION correspondence.reject_unauthorized_mutation();

                CREATE TRIGGER trg_letter_status_history_no_delete
                    BEFORE DELETE ON correspondence.letter_status_history
                    FOR EACH ROW EXECUTE FUNCTION correspondence.reject_unauthorized_mutation();
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DROP TRIGGER IF EXISTS trg_letter_status_history_no_delete ON correspondence.letter_status_history;
                DROP TRIGGER IF EXISTS trg_letter_status_history_no_update ON correspondence.letter_status_history;
                DROP TRIGGER IF EXISTS trg_letters_no_delete ON correspondence.letters;
                DROP FUNCTION IF EXISTS correspondence.reject_unauthorized_mutation();
                """);

            migrationBuilder.DropTable(
                name: "export_activity",
                schema: "correspondence");

            migrationBuilder.DropTable(
                name: "letter_attachments",
                schema: "correspondence");

            migrationBuilder.DropTable(
                name: "letter_delivery_records",
                schema: "correspondence");

            migrationBuilder.DropTable(
                name: "letter_documents",
                schema: "correspondence");

            migrationBuilder.DropTable(
                name: "letter_holds",
                schema: "correspondence");

            migrationBuilder.DropTable(
                name: "letter_recipients",
                schema: "correspondence");

            migrationBuilder.DropTable(
                name: "letter_status_history",
                schema: "correspondence");

            migrationBuilder.DropTable(
                name: "organization_unit_references",
                schema: "correspondence");

            migrationBuilder.DropTable(
                name: "OutboxMessage",
                schema: "correspondence");

            migrationBuilder.DropTable(
                name: "templates",
                schema: "correspondence");

            migrationBuilder.DropTable(
                name: "letters",
                schema: "correspondence");

            migrationBuilder.DropTable(
                name: "InboxState",
                schema: "correspondence");

            migrationBuilder.DropTable(
                name: "OutboxState",
                schema: "correspondence");
        }
    }
}
