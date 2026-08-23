using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace CommunityOS.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreateAudit : Migration
    {
        private static readonly string[] InboxMessageForeignKeyColumns = ["MessageId", "ConsumerId"];
        private static readonly string[] OutboxMessageInboxIndexColumns = ["InboxMessageId", "InboxConsumerId", "SequenceNumber"];
        private static readonly string[] OutboxMessageOutboxIndexColumns = ["OutboxId", "SequenceNumber"];
        private static readonly string[] RetentionExpiryIndexColumns = ["retention_class", "retention_expires_on"];
        private static readonly string[] ResourceIndexColumns = ["resource_type", "resource_id"];

        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "audit");

            migrationBuilder.CreateTable(
                name: "audit_entries",
                schema: "audit",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    occurred_on = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ingested_on = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    source_service = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    source_event_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    source_event_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    action = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    outcome = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    resource_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    resource_id = table.Column<Guid>(type: "uuid", nullable: false),
                    secondary_resource_id = table.Column<Guid>(type: "uuid", nullable: true),
                    subject_id = table.Column<Guid>(type: "uuid", nullable: true),
                    actor_id = table.Column<Guid>(type: "uuid", nullable: true),
                    organization_unit_id = table.Column<Guid>(type: "uuid", nullable: true),
                    sensitivity = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    correlation_id = table.Column<Guid>(type: "uuid", nullable: true),
                    causation_id = table.Column<Guid>(type: "uuid", nullable: true),
                    metadata = table.Column<string>(type: "jsonb", nullable: true),
                    retention_class = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    retention_expires_on = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_audit_entries", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "InboxState",
                schema: "audit",
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
                schema: "audit",
                columns: table => new
                {
                    organization_unit_id = table.Column<Guid>(type: "uuid", nullable: false),
                    parent_organization_unit_id = table.Column<Guid>(type: "uuid", nullable: true),
                    last_updated_on = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_organization_unit_references", x => x.organization_unit_id);
                });

            migrationBuilder.CreateTable(
                name: "OutboxState",
                schema: "audit",
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
                name: "audit_entry_holds",
                schema: "audit",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    entry_id = table.Column<Guid>(type: "uuid", nullable: false),
                    hold_type = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    placed_by = table.Column<Guid>(type: "uuid", nullable: false),
                    placed_on = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    released_by = table.Column<Guid>(type: "uuid", nullable: true),
                    released_on = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    reason_code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_audit_entry_holds", x => x.id);
                    table.ForeignKey(
                        name: "FK_audit_entry_holds_audit_entries_entry_id",
                        column: x => x.entry_id,
                        principalSchema: "audit",
                        principalTable: "audit_entries",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "OutboxMessage",
                schema: "audit",
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
                        principalSchema: "audit",
                        principalTable: "InboxState",
                        principalColumns: InboxMessageForeignKeyColumns);
                    table.ForeignKey(
                        name: "FK_OutboxMessage_OutboxState_OutboxId",
                        column: x => x.OutboxId,
                        principalSchema: "audit",
                        principalTable: "OutboxState",
                        principalColumn: "OutboxId");
                });

            migrationBuilder.CreateIndex(
                name: "ix_audit_entries_actor_id",
                schema: "audit",
                table: "audit_entries",
                column: "actor_id");

            migrationBuilder.CreateIndex(
                name: "ix_audit_entries_organization_unit_id",
                schema: "audit",
                table: "audit_entries",
                column: "organization_unit_id");

            migrationBuilder.CreateIndex(
                name: "ix_audit_entries_resource",
                schema: "audit",
                table: "audit_entries",
                columns: ResourceIndexColumns);

            migrationBuilder.CreateIndex(
                name: "ix_audit_entries_retention_expiry",
                schema: "audit",
                table: "audit_entries",
                columns: RetentionExpiryIndexColumns,
                filter: "retention_expires_on IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_audit_entries_sensitivity",
                schema: "audit",
                table: "audit_entries",
                column: "sensitivity");

            migrationBuilder.CreateIndex(
                name: "ix_audit_entries_subject_id",
                schema: "audit",
                table: "audit_entries",
                column: "subject_id");

            migrationBuilder.CreateIndex(
                name: "ux_audit_entries_source_event_hash",
                schema: "audit",
                table: "audit_entries",
                column: "source_event_hash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_audit_entry_holds_active",
                schema: "audit",
                table: "audit_entry_holds",
                column: "entry_id",
                filter: "released_on IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_InboxState_Delivered",
                schema: "audit",
                table: "InboxState",
                column: "Delivered");

            migrationBuilder.CreateIndex(
                name: "IX_OutboxMessage_EnqueueTime",
                schema: "audit",
                table: "OutboxMessage",
                column: "EnqueueTime");

            migrationBuilder.CreateIndex(
                name: "IX_OutboxMessage_ExpirationTime",
                schema: "audit",
                table: "OutboxMessage",
                column: "ExpirationTime");

            migrationBuilder.CreateIndex(
                name: "IX_OutboxMessage_InboxMessageId_InboxConsumerId_SequenceNumber",
                schema: "audit",
                table: "OutboxMessage",
                columns: OutboxMessageInboxIndexColumns,
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OutboxMessage_OutboxId_SequenceNumber",
                schema: "audit",
                table: "OutboxMessage",
                columns: OutboxMessageOutboxIndexColumns,
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OutboxState_Created",
                schema: "audit",
                table: "OutboxState",
                column: "Created");

            // Database-level immutability (ADR-027 decision 2): native
            // triggers reject UPDATE/DELETE on audit_entries unless the session
            // sets app.audit_purge_authorized = 'on' via SET LOCAL. Only the
            // ratified purge operation flips the guard.
            migrationBuilder.Sql("""
                CREATE FUNCTION audit.reject_unauthorized_mutation() RETURNS trigger AS $$
                BEGIN
                    IF current_setting('app.audit_purge_authorized', true) IS DISTINCT FROM 'on' THEN
                        RAISE EXCEPTION 'audit_entries is append-only: % blocked (set app.audit_purge_authorized to enable purge)',
                            TG_OP;
                    END IF;
                    RETURN COALESCE(NEW, OLD);
                END;
                $$ LANGUAGE plpgsql;

                CREATE TRIGGER trg_audit_entries_no_update
                    BEFORE UPDATE ON audit.audit_entries
                    FOR EACH ROW EXECUTE FUNCTION audit.reject_unauthorized_mutation();

                CREATE TRIGGER trg_audit_entries_no_delete
                    BEFORE DELETE ON audit.audit_entries
                    FOR EACH ROW EXECUTE FUNCTION audit.reject_unauthorized_mutation();
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DROP TRIGGER IF EXISTS trg_audit_entries_no_update ON audit.audit_entries;
                DROP TRIGGER IF EXISTS trg_audit_entries_no_delete ON audit.audit_entries;
                DROP FUNCTION IF EXISTS audit.reject_unauthorized_mutation();
                """);

            migrationBuilder.DropTable(
                name: "audit_entry_holds",
                schema: "audit");

            migrationBuilder.DropTable(
                name: "organization_unit_references",
                schema: "audit");

            migrationBuilder.DropTable(
                name: "OutboxMessage",
                schema: "audit");

            migrationBuilder.DropTable(
                name: "audit_entries",
                schema: "audit");

            migrationBuilder.DropTable(
                name: "InboxState",
                schema: "audit");

            migrationBuilder.DropTable(
                name: "OutboxState",
                schema: "audit");
        }
    }
}
