using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace CommunityOS.Localization.Infrastructure.Persistence
{
    /// <inheritdoc />
    public partial class InitialCreateLocalization : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "localization");

            migrationBuilder.CreateTable(
                name: "catalog_state",
                schema: "localization",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    version = table.Column<long>(type: "bigint", nullable: false),
                    updated_on = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_catalog_state", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "entity_translations",
                schema: "localization",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    source_context = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    entity_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    entity_id = table.Column<Guid>(type: "uuid", nullable: false),
                    field = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    culture_code = table.Column<string>(type: "character varying(35)", maxLength: 35, nullable: false),
                    is_deprecated = table.Column<bool>(type: "boolean", nullable: false),
                    deprecated_on = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    revision = table.Column<int>(type: "integer", nullable: false),
                    created_on = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_on = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_entity_translations", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "InboxState",
                schema: "localization",
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
                name: "locales",
                schema: "localization",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(35)", maxLength: 35, nullable: false),
                    display_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    is_default = table.Column<bool>(type: "boolean", nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    created_on = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_on = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_locales", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "namespaces",
                schema: "localization",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    created_on = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedOn = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_namespaces", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "OutboxState",
                schema: "localization",
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
                name: "resource_entries",
                schema: "localization",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    namespace_id = table.Column<Guid>(type: "uuid", nullable: false),
                    key = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    is_deprecated = table.Column<bool>(type: "boolean", nullable: false),
                    deprecated_on = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    revision = table.Column<int>(type: "integer", nullable: false),
                    created_on = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_on = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_resource_entries", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "translation_suggestions",
                schema: "localization",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    target_kind = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    target_id = table.Column<Guid>(type: "uuid", nullable: false),
                    target_culture_code = table.Column<string>(type: "character varying(35)", maxLength: 35, nullable: false),
                    suggested_value = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    provenance = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    status = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    decided_by = table.Column<Guid>(type: "uuid", nullable: true),
                    decided_on = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_on = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_translation_suggestions", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "entity_translation_revisions",
                schema: "localization",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    translation_id = table.Column<Guid>(type: "uuid", nullable: false),
                    culture_code = table.Column<string>(type: "character varying(35)", maxLength: 35, nullable: false),
                    value = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    state = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    provenance = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    proposed_by = table.Column<Guid>(type: "uuid", nullable: false),
                    proposed_on = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    reviewed_by = table.Column<Guid>(type: "uuid", nullable: true),
                    reviewed_on = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_entity_translation_revisions", x => x.id);
                    table.ForeignKey(
                        name: "FK_entity_translation_revisions_entity_translations_translatio~",
                        column: x => x.translation_id,
                        principalSchema: "localization",
                        principalTable: "entity_translations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "OutboxMessage",
                schema: "localization",
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
                        principalSchema: "localization",
                        principalTable: "InboxState",
                        principalColumns: new[] { "MessageId", "ConsumerId" });
                    table.ForeignKey(
                        name: "FK_OutboxMessage_OutboxState_OutboxId",
                        column: x => x.OutboxId,
                        principalSchema: "localization",
                        principalTable: "OutboxState",
                        principalColumn: "OutboxId");
                });

            migrationBuilder.CreateTable(
                name: "resource_revisions",
                schema: "localization",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    entry_id = table.Column<Guid>(type: "uuid", nullable: false),
                    culture_code = table.Column<string>(type: "character varying(35)", maxLength: 35, nullable: false),
                    value = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    state = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    provenance = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    proposed_by = table.Column<Guid>(type: "uuid", nullable: false),
                    proposed_on = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    reviewed_by = table.Column<Guid>(type: "uuid", nullable: true),
                    reviewed_on = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_resource_revisions", x => x.id);
                    table.ForeignKey(
                        name: "FK_resource_revisions_resource_entries_entry_id",
                        column: x => x.entry_id,
                        principalSchema: "localization",
                        principalTable: "resource_entries",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                schema: "localization",
                table: "catalog_state",
                columns: new[] { "id", "updated_on", "version" },
                values: new object[] { new Guid("a1b2c3d4-e5f6-47a8-9b0c-1d2e3f4a5b6d"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), 0L });

            migrationBuilder.InsertData(
                schema: "localization",
                table: "locales",
                columns: new[] { "id", "code", "created_by", "created_on", "display_name", "is_default", "status", "updated_on" },
                values: new object[] { new Guid("3f2a9c1e-6b7d-4c8e-9a0f-1d2e3f4a5b6c"), "en", new Guid("00000000-0000-0000-0000-000000000001"), new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "English", true, "active", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) });

            migrationBuilder.CreateIndex(
                name: "ux_entity_translation_revisions_translation_state_approved",
                schema: "localization",
                table: "entity_translation_revisions",
                columns: new[] { "translation_id", "state" },
                unique: true,
                filter: "state = 'approved'");

            migrationBuilder.CreateIndex(
                name: "ux_entity_translations_tuple",
                schema: "localization",
                table: "entity_translations",
                columns: new[] { "source_context", "entity_type", "entity_id", "field", "culture_code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_InboxState_Delivered",
                schema: "localization",
                table: "InboxState",
                column: "Delivered");

            migrationBuilder.CreateIndex(
                name: "ux_locales_code",
                schema: "localization",
                table: "locales",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ux_namespaces_name",
                schema: "localization",
                table: "namespaces",
                column: "name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OutboxMessage_EnqueueTime",
                schema: "localization",
                table: "OutboxMessage",
                column: "EnqueueTime");

            migrationBuilder.CreateIndex(
                name: "IX_OutboxMessage_ExpirationTime",
                schema: "localization",
                table: "OutboxMessage",
                column: "ExpirationTime");

            migrationBuilder.CreateIndex(
                name: "IX_OutboxMessage_InboxMessageId_InboxConsumerId_SequenceNumber",
                schema: "localization",
                table: "OutboxMessage",
                columns: new[] { "InboxMessageId", "InboxConsumerId", "SequenceNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OutboxMessage_OutboxId_SequenceNumber",
                schema: "localization",
                table: "OutboxMessage",
                columns: new[] { "OutboxId", "SequenceNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OutboxState_Created",
                schema: "localization",
                table: "OutboxState",
                column: "Created");

            migrationBuilder.CreateIndex(
                name: "ux_resource_entries_namespace_key",
                schema: "localization",
                table: "resource_entries",
                columns: new[] { "namespace_id", "key" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_resource_revisions_state_proposed_on",
                schema: "localization",
                table: "resource_revisions",
                columns: new[] { "state", "proposed_on" });

            migrationBuilder.CreateIndex(
                name: "ux_resource_revisions_entry_culture_approved",
                schema: "localization",
                table: "resource_revisions",
                columns: new[] { "entry_id", "culture_code", "state" },
                unique: true,
                filter: "state = 'approved'");

            migrationBuilder.CreateIndex(
                name: "ix_translation_suggestions_status_created_on",
                schema: "localization",
                table: "translation_suggestions",
                columns: new[] { "status", "created_on" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "catalog_state",
                schema: "localization");

            migrationBuilder.DropTable(
                name: "entity_translation_revisions",
                schema: "localization");

            migrationBuilder.DropTable(
                name: "locales",
                schema: "localization");

            migrationBuilder.DropTable(
                name: "namespaces",
                schema: "localization");

            migrationBuilder.DropTable(
                name: "OutboxMessage",
                schema: "localization");

            migrationBuilder.DropTable(
                name: "resource_revisions",
                schema: "localization");

            migrationBuilder.DropTable(
                name: "translation_suggestions",
                schema: "localization");

            migrationBuilder.DropTable(
                name: "entity_translations",
                schema: "localization");

            migrationBuilder.DropTable(
                name: "InboxState",
                schema: "localization");

            migrationBuilder.DropTable(
                name: "OutboxState",
                schema: "localization");

            migrationBuilder.DropTable(
                name: "resource_entries",
                schema: "localization");
        }
    }
}
