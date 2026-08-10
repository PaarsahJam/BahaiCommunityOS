using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CommunityOS.Migrations
{
    /// <inheritdoc />
    public partial class AddAuthorizationInfrastructure : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "authorization");

            migrationBuilder.CreateTable(
                name: "authorization_relationships",
                schema: "authorization",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    subject_id = table.Column<Guid>(type: "uuid", nullable: false),
                    relation = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    object_type = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    object_id = table.Column<Guid>(type: "uuid", nullable: false),
                    permissions = table.Column<string[]>(type: "text[]", nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_authorization_relationships", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "break_glass_requests",
                schema: "authorization",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    requester_id = table.Column<Guid>(type: "uuid", nullable: false),
                    scope_type = table.Column<int>(type: "integer", nullable: false),
                    scope_id = table.Column<Guid>(type: "uuid", nullable: true),
                    scope_resource_type = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: false),
                    requested_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    requested_duration_minutes = table.Column<int>(type: "integer", nullable: false),
                    state = table.Column<int>(type: "integer", nullable: false),
                    approver_id = table.Column<Guid>(type: "uuid", nullable: true),
                    approved_on = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    approved_until = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    rejection_reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    revoked_by = table.Column<Guid>(type: "uuid", nullable: true),
                    revoked_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    revocation_reason = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    permissions = table.Column<string[]>(type: "text[]", nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_break_glass_requests", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "delegations",
                schema: "authorization",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    delegator_id = table.Column<Guid>(type: "uuid", nullable: false),
                    delegate_id = table.Column<Guid>(type: "uuid", nullable: false),
                    scope_type = table.Column<int>(type: "integer", nullable: false),
                    scope_id = table.Column<Guid>(type: "uuid", nullable: true),
                    scope_resource_type = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    starts_on = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    expires_on = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    revoked_by = table.Column<Guid>(type: "uuid", nullable: true),
                    revoked_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    revocation_reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    permissions = table.Column<string[]>(type: "text[]", nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_delegations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "role_assignments",
                schema: "authorization",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    subject_id = table.Column<Guid>(type: "uuid", nullable: false),
                    role_id = table.Column<Guid>(type: "uuid", nullable: false),
                    role_code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    scope_type = table.Column<int>(type: "integer", nullable: false),
                    scope_id = table.Column<Guid>(type: "uuid", nullable: true),
                    scope_resource_type = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true),
                    granted_by = table.Column<Guid>(type: "uuid", nullable: false),
                    granted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    effective_from = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    effective_until = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    revoked_by = table.Column<Guid>(type: "uuid", nullable: true),
                    revoked_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    revocation_reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_role_assignments", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "roles",
                schema: "authorization",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    display_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    enabled = table.Column<bool>(type: "boolean", nullable: false),
                    is_system = table.Column<bool>(type: "boolean", nullable: false),
                    created_on = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    permissions = table.Column<string[]>(type: "text[]", nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_roles", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_authorization_relationships_object_type_object_id",
                schema: "authorization",
                table: "authorization_relationships",
                columns: new[] { "object_type", "object_id" });

            migrationBuilder.CreateIndex(
                name: "IX_authorization_relationships_subject_id_relation",
                schema: "authorization",
                table: "authorization_relationships",
                columns: new[] { "subject_id", "relation" });

            migrationBuilder.CreateIndex(
                name: "IX_break_glass_requests_requester_id",
                schema: "authorization",
                table: "break_glass_requests",
                column: "requester_id");

            migrationBuilder.CreateIndex(
                name: "IX_break_glass_requests_state",
                schema: "authorization",
                table: "break_glass_requests",
                column: "state");

            migrationBuilder.CreateIndex(
                name: "IX_delegations_delegate_id",
                schema: "authorization",
                table: "delegations",
                column: "delegate_id");

            migrationBuilder.CreateIndex(
                name: "IX_delegations_delegator_id",
                schema: "authorization",
                table: "delegations",
                column: "delegator_id");

            migrationBuilder.CreateIndex(
                name: "IX_role_assignments_role_id_subject_id",
                schema: "authorization",
                table: "role_assignments",
                columns: new[] { "role_id", "subject_id" });

            migrationBuilder.CreateIndex(
                name: "IX_role_assignments_subject_id",
                schema: "authorization",
                table: "role_assignments",
                column: "subject_id");

            migrationBuilder.CreateIndex(
                name: "IX_roles_code",
                schema: "authorization",
                table: "roles",
                column: "code",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "authorization_relationships",
                schema: "authorization");

            migrationBuilder.DropTable(
                name: "break_glass_requests",
                schema: "authorization");

            migrationBuilder.DropTable(
                name: "delegations",
                schema: "authorization");

            migrationBuilder.DropTable(
                name: "role_assignments",
                schema: "authorization");

            migrationBuilder.DropTable(
                name: "roles",
                schema: "authorization");
        }
    }
}
