using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CommunityOS.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreateOrganization : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "organization");

            migrationBuilder.CreateTable(
                name: "appointments",
                schema: "organization",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    person_id = table.Column<Guid>(type: "uuid", nullable: false),
                    organization_unit_id = table.Column<Guid>(type: "uuid", nullable: false),
                    appointment_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    effective_from = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    effective_until = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    status = table.Column<int>(type: "integer", nullable: false),
                    assigned_by = table.Column<Guid>(type: "uuid", nullable: true),
                    assigned_on = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ended_by = table.Column<Guid>(type: "uuid", nullable: true),
                    ended_on = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_appointments", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "committees",
                schema: "organization",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    committee_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    organization_unit_id = table.Column<Guid>(type: "uuid", nullable: true),
                    jurisdiction_type = table.Column<int>(type: "integer", nullable: false),
                    jurisdiction_scope_id = table.Column<Guid>(type: "uuid", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_on = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_committees", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "delegation_facts",
                schema: "organization",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    delegator_id = table.Column<Guid>(type: "uuid", nullable: false),
                    delegate_id = table.Column<Guid>(type: "uuid", nullable: false),
                    organization_unit_id = table.Column<Guid>(type: "uuid", nullable: false),
                    delegation_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    effective_from = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    effective_until = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    status = table.Column<int>(type: "integer", nullable: false),
                    granted_by = table.Column<Guid>(type: "uuid", nullable: false),
                    granted_on = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    revoked_by = table.Column<Guid>(type: "uuid", nullable: true),
                    revoked_on = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_delegation_facts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "institutions",
                schema: "organization",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    institution_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    jurisdiction_type = table.Column<int>(type: "integer", nullable: false),
                    jurisdiction_scope_id = table.Column<Guid>(type: "uuid", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    established_on = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_on = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_institutions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "organization_units",
                schema: "organization",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    organization_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    unit_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    created_on = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_organization_units", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "organizations",
                schema: "organization",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    organization_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    jurisdiction_type = table.Column<int>(type: "integer", nullable: false),
                    jurisdiction_scope_id = table.Column<Guid>(type: "uuid", nullable: true),
                    status = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    established_on = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    dissolved_on = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_on = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_organizations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "committee_members",
                schema: "organization",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    person_id = table.Column<Guid>(type: "uuid", nullable: false),
                    role_code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    effective_from = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    effective_until = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    committee_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_committee_members", x => x.Id);
                    table.ForeignKey(
                        name: "FK_committee_members_committees_committee_id",
                        column: x => x.committee_id,
                        principalSchema: "organization",
                        principalTable: "committees",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "organization_unit_parents",
                schema: "organization",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    parent_id = table.Column<Guid>(type: "uuid", nullable: true),
                    effective_from = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    effective_until = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    unit_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_organization_unit_parents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_organization_unit_parents_organization_units_unit_id",
                        column: x => x.unit_id,
                        principalSchema: "organization",
                        principalTable: "organization_units",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_appointments_organization_unit_id",
                schema: "organization",
                table: "appointments",
                column: "organization_unit_id");

            migrationBuilder.CreateIndex(
                name: "IX_appointments_person_id",
                schema: "organization",
                table: "appointments",
                column: "person_id");

            migrationBuilder.CreateIndex(
                name: "IX_appointments_person_id_organization_unit_id_appointment_type",
                schema: "organization",
                table: "appointments",
                columns: new[] { "person_id", "organization_unit_id", "appointment_type" });

            migrationBuilder.CreateIndex(
                name: "IX_committee_members_committee_id",
                schema: "organization",
                table: "committee_members",
                column: "committee_id");

            migrationBuilder.CreateIndex(
                name: "IX_committee_members_person_id",
                schema: "organization",
                table: "committee_members",
                column: "person_id");

            migrationBuilder.CreateIndex(
                name: "IX_committees_organization_id",
                schema: "organization",
                table: "committees",
                column: "organization_id");

            migrationBuilder.CreateIndex(
                name: "IX_committees_organization_unit_id",
                schema: "organization",
                table: "committees",
                column: "organization_unit_id");

            migrationBuilder.CreateIndex(
                name: "IX_delegation_facts_delegate_id",
                schema: "organization",
                table: "delegation_facts",
                column: "delegate_id");

            migrationBuilder.CreateIndex(
                name: "IX_delegation_facts_delegator_id",
                schema: "organization",
                table: "delegation_facts",
                column: "delegator_id");

            migrationBuilder.CreateIndex(
                name: "IX_delegation_facts_organization_unit_id",
                schema: "organization",
                table: "delegation_facts",
                column: "organization_unit_id");

            migrationBuilder.CreateIndex(
                name: "IX_institutions_name",
                schema: "organization",
                table: "institutions",
                column: "name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_organization_unit_parents_parent_id",
                schema: "organization",
                table: "organization_unit_parents",
                column: "parent_id");

            migrationBuilder.CreateIndex(
                name: "IX_organization_unit_parents_unit_id",
                schema: "organization",
                table: "organization_unit_parents",
                column: "unit_id");

            migrationBuilder.CreateIndex(
                name: "IX_organization_units_organization_id",
                schema: "organization",
                table: "organization_units",
                column: "organization_id");

            migrationBuilder.CreateIndex(
                name: "IX_organization_units_organization_id_name",
                schema: "organization",
                table: "organization_units",
                columns: new[] { "organization_id", "name" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_organizations_name",
                schema: "organization",
                table: "organizations",
                column: "name",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "appointments",
                schema: "organization");

            migrationBuilder.DropTable(
                name: "committee_members",
                schema: "organization");

            migrationBuilder.DropTable(
                name: "delegation_facts",
                schema: "organization");

            migrationBuilder.DropTable(
                name: "institutions",
                schema: "organization");

            migrationBuilder.DropTable(
                name: "organization_unit_parents",
                schema: "organization");

            migrationBuilder.DropTable(
                name: "organizations",
                schema: "organization");

            migrationBuilder.DropTable(
                name: "committees",
                schema: "organization");

            migrationBuilder.DropTable(
                name: "organization_units",
                schema: "organization");
        }
    }
}
