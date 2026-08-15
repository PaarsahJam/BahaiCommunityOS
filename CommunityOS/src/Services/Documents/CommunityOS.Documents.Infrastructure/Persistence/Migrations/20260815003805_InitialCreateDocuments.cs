using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CommunityOS.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreateDocuments : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "documents");

            migrationBuilder.CreateTable(
                name: "documents",
                schema: "documents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    title = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    status = table.Column<int>(type: "integer", nullable: false),
                    organization_unit_id = table.Column<Guid>(type: "uuid", nullable: true),
                    owner_type = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: true),
                    owner_id = table.Column<Guid>(type: "uuid", nullable: true),
                    current_version_id = table.Column<Guid>(type: "uuid", nullable: true),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    created_on = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_by = table.Column<Guid>(type: "uuid", nullable: false),
                    updated_on = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_documents", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "organization_unit_references",
                schema: "documents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    unit_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    parent_id = table.Column<Guid>(type: "uuid", nullable: true),
                    last_seen_on = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_organization_unit_references", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "document_classification",
                schema: "documents",
                columns: table => new
                {
                    document_id = table.Column<Guid>(type: "uuid", nullable: false),
                    classification_code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    is_sensitive = table.Column<bool>(type: "boolean", nullable: false),
                    retention_category = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    legal_hold_reference = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    administrative_hold_reference = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    classified_by = table.Column<Guid>(type: "uuid", nullable: true),
                    classified_on = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_document_classification", x => x.document_id);
                    table.ForeignKey(
                        name: "FK_document_classification_documents_document_id",
                        column: x => x.document_id,
                        principalSchema: "documents",
                        principalTable: "documents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "document_references",
                schema: "documents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    source_context = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    source_entity_id = table.Column<Guid>(type: "uuid", nullable: false),
                    reference_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    created_by = table.Column<Guid>(type: "uuid", nullable: false),
                    created_on = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    document_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_document_references", x => x.Id);
                    table.ForeignKey(
                        name: "FK_document_references_documents_document_id",
                        column: x => x.document_id,
                        principalSchema: "documents",
                        principalTable: "documents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "document_scopes",
                schema: "documents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    organization_unit_id = table.Column<Guid>(type: "uuid", nullable: false),
                    document_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_document_scopes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_document_scopes_documents_document_id",
                        column: x => x.document_id,
                        principalSchema: "documents",
                        principalTable: "documents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "document_versions",
                schema: "documents",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    version_number = table.Column<int>(type: "integer", nullable: false),
                    content_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    object_key = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    mime_type = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    size_bytes = table.Column<long>(type: "bigint", nullable: false),
                    file_name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    source = table.Column<string>(type: "character varying(30)", maxLength: 30, nullable: false),
                    uploaded_by = table.Column<Guid>(type: "uuid", nullable: false),
                    uploaded_on = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    scan_status = table.Column<int>(type: "integer", nullable: false),
                    document_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_document_versions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_document_versions_documents_document_id",
                        column: x => x.document_id,
                        principalSchema: "documents",
                        principalTable: "documents",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_document_references_context_entity_type",
                schema: "documents",
                table: "document_references",
                columns: new[] { "document_id", "source_context", "source_entity_id", "reference_type" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_document_scopes_document_organization_unit",
                schema: "documents",
                table: "document_scopes",
                columns: new[] { "document_id", "organization_unit_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_document_versions_document_version_number",
                schema: "documents",
                table: "document_versions",
                columns: new[] { "document_id", "version_number" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_documents_organization_unit_id",
                schema: "documents",
                table: "documents",
                column: "organization_unit_id");

            migrationBuilder.CreateIndex(
                name: "ix_organization_unit_references_organization_id",
                schema: "documents",
                table: "organization_unit_references",
                column: "organization_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "document_classification",
                schema: "documents");

            migrationBuilder.DropTable(
                name: "document_references",
                schema: "documents");

            migrationBuilder.DropTable(
                name: "document_scopes",
                schema: "documents");

            migrationBuilder.DropTable(
                name: "document_versions",
                schema: "documents");

            migrationBuilder.DropTable(
                name: "organization_unit_references",
                schema: "documents");

            migrationBuilder.DropTable(
                name: "documents",
                schema: "documents");
        }
    }
}
