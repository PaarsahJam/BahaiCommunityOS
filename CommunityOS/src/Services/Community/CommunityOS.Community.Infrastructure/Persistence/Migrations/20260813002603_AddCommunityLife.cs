using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CommunityOS.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCommunityLife : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "activities",
                schema: "community",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    category = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    organizer_person_id = table.Column<Guid>(type: "uuid", nullable: true),
                    organization_unit_id = table.Column<Guid>(type: "uuid", nullable: true),
                    location = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    is_online = table.Column<bool>(type: "boolean", nullable: false),
                    online_url = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    starts_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ends_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    visibility = table.Column<int>(type: "integer", nullable: false),
                    status = table.Column<int>(type: "integer", nullable: false),
                    capacity = table.Column<int>(type: "integer", nullable: true),
                    created_on = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_activities", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "community_events",
                schema: "community",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    starts_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ends_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    time_zone = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    location = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    is_online = table.Column<bool>(type: "boolean", nullable: false),
                    online_url = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    organizer_person_id = table.Column<Guid>(type: "uuid", nullable: true),
                    organization_unit_id = table.Column<Guid>(type: "uuid", nullable: true),
                    status = table.Column<int>(type: "integer", nullable: false),
                    visibility = table.Column<int>(type: "integer", nullable: false),
                    registration_open = table.Column<bool>(type: "boolean", nullable: false),
                    capacity = table.Column<int>(type: "integer", nullable: true),
                    created_on = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_community_events", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "family_relationships",
                schema: "community",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    person_id_a = table.Column<Guid>(type: "uuid", nullable: false),
                    person_id_b = table.Column<Guid>(type: "uuid", nullable: false),
                    relationship_type = table.Column<int>(type: "integer", nullable: false),
                    effective_from = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    effective_until = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_family_relationships", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "households",
                schema: "community",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    address_line1 = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    address_line2 = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    address_city = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    address_region = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    address_postal_code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    address_country = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_households", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "meetings",
                schema: "community",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    starts_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ends_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    time_zone = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    location = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    organizer_person_id = table.Column<Guid>(type: "uuid", nullable: true),
                    organization_unit_id = table.Column<Guid>(type: "uuid", nullable: true),
                    status = table.Column<int>(type: "integer", nullable: false),
                    visibility = table.Column<int>(type: "integer", nullable: false),
                    minutes = table.Column<string>(type: "character varying(20000)", maxLength: 20000, nullable: true),
                    created_on = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_meetings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "memberships",
                schema: "community",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    person_id = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<int>(type: "integer", nullable: false),
                    effective_from = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    effective_until = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    withdrawn_on = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_memberships", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "participations",
                schema: "community",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    person_id = table.Column<Guid>(type: "uuid", nullable: false),
                    target_type = table.Column<int>(type: "integer", nullable: false),
                    target_id = table.Column<Guid>(type: "uuid", nullable: false),
                    role = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    status = table.Column<int>(type: "integer", nullable: false),
                    effective_from = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    effective_until = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    recorded_on = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_participations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "persons",
                schema: "community",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    preferred_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    formal_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    date_of_birth = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    preferred_language = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    identity_account_id = table.Column<Guid>(type: "uuid", nullable: true),
                    identity_linked_on = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    identity_unlinked_on = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    profile_visibility = table.Column<int>(type: "integer", nullable: false),
                    contact_visibility = table.Column<int>(type: "integer", nullable: false),
                    date_of_birth_visibility = table.Column<int>(type: "integer", nullable: false),
                    created_on = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_persons", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "household_members",
                schema: "community",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    person_id = table.Column<Guid>(type: "uuid", nullable: false),
                    role = table.Column<int>(type: "integer", nullable: false),
                    effective_from = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    effective_until = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    household_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_household_members", x => x.Id);
                    table.ForeignKey(
                        name: "FK_household_members_households_household_id",
                        column: x => x.household_id,
                        principalSchema: "community",
                        principalTable: "households",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "meeting_actions",
                schema: "community",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    assignee_person_id = table.Column<Guid>(type: "uuid", nullable: true),
                    due_date = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    is_completed = table.Column<bool>(type: "boolean", nullable: false),
                    completed_on = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    meeting_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_meeting_actions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_meeting_actions_meetings_meeting_id",
                        column: x => x.meeting_id,
                        principalSchema: "community",
                        principalTable: "meetings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "meeting_agenda_items",
                schema: "community",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    order = table.Column<int>(type: "integer", nullable: false),
                    is_completed = table.Column<bool>(type: "boolean", nullable: false),
                    meeting_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_meeting_agenda_items", x => x.Id);
                    table.ForeignKey(
                        name: "FK_meeting_agenda_items_meetings_meeting_id",
                        column: x => x.meeting_id,
                        principalSchema: "community",
                        principalTable: "meetings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "meeting_participants",
                schema: "community",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    person_id = table.Column<Guid>(type: "uuid", nullable: false),
                    role = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    attendance = table.Column<int>(type: "integer", nullable: false),
                    meeting_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_meeting_participants", x => x.Id);
                    table.ForeignKey(
                        name: "FK_meeting_participants_meetings_meeting_id",
                        column: x => x.meeting_id,
                        principalSchema: "community",
                        principalTable: "meetings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "membership_periods",
                schema: "community",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    status = table.Column<int>(type: "integer", nullable: false),
                    effective_from = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    effective_until = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    membership_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_membership_periods", x => x.Id);
                    table.ForeignKey(
                        name: "FK_membership_periods_memberships_membership_id",
                        column: x => x.membership_id,
                        principalSchema: "community",
                        principalTable: "memberships",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "contact_methods",
                schema: "community",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    type = table.Column<int>(type: "integer", nullable: false),
                    value = table.Column<string>(type: "character varying(320)", maxLength: 320, nullable: false),
                    is_preferred = table.Column<bool>(type: "boolean", nullable: false),
                    visibility = table.Column<int>(type: "integer", nullable: false),
                    person_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_contact_methods", x => x.Id);
                    table.ForeignKey(
                        name: "FK_contact_methods_persons_person_id",
                        column: x => x.person_id,
                        principalSchema: "community",
                        principalTable: "persons",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_activities_organization_unit_id",
                schema: "community",
                table: "activities",
                column: "organization_unit_id");

            migrationBuilder.CreateIndex(
                name: "IX_community_events_organization_unit_id",
                schema: "community",
                table: "community_events",
                column: "organization_unit_id");

            migrationBuilder.CreateIndex(
                name: "IX_contact_methods_person_id",
                schema: "community",
                table: "contact_methods",
                column: "person_id");

            migrationBuilder.CreateIndex(
                name: "IX_family_relationships_person_id_a_person_id_b",
                schema: "community",
                table: "family_relationships",
                columns: new[] { "person_id_a", "person_id_b" });

            migrationBuilder.CreateIndex(
                name: "IX_household_members_household_id",
                schema: "community",
                table: "household_members",
                column: "household_id");

            migrationBuilder.CreateIndex(
                name: "IX_meeting_actions_meeting_id",
                schema: "community",
                table: "meeting_actions",
                column: "meeting_id");

            migrationBuilder.CreateIndex(
                name: "IX_meeting_agenda_items_meeting_id",
                schema: "community",
                table: "meeting_agenda_items",
                column: "meeting_id");

            migrationBuilder.CreateIndex(
                name: "IX_meeting_participants_meeting_id",
                schema: "community",
                table: "meeting_participants",
                column: "meeting_id");

            migrationBuilder.CreateIndex(
                name: "IX_meetings_organization_unit_id",
                schema: "community",
                table: "meetings",
                column: "organization_unit_id");

            migrationBuilder.CreateIndex(
                name: "IX_membership_periods_membership_id",
                schema: "community",
                table: "membership_periods",
                column: "membership_id");

            migrationBuilder.CreateIndex(
                name: "IX_memberships_person_id",
                schema: "community",
                table: "memberships",
                column: "person_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_participations_person_id",
                schema: "community",
                table: "participations",
                column: "person_id");

            migrationBuilder.CreateIndex(
                name: "IX_participations_target_type_target_id",
                schema: "community",
                table: "participations",
                columns: new[] { "target_type", "target_id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "activities",
                schema: "community");

            migrationBuilder.DropTable(
                name: "community_events",
                schema: "community");

            migrationBuilder.DropTable(
                name: "contact_methods",
                schema: "community");

            migrationBuilder.DropTable(
                name: "family_relationships",
                schema: "community");

            migrationBuilder.DropTable(
                name: "household_members",
                schema: "community");

            migrationBuilder.DropTable(
                name: "meeting_actions",
                schema: "community");

            migrationBuilder.DropTable(
                name: "meeting_agenda_items",
                schema: "community");

            migrationBuilder.DropTable(
                name: "meeting_participants",
                schema: "community");

            migrationBuilder.DropTable(
                name: "membership_periods",
                schema: "community");

            migrationBuilder.DropTable(
                name: "participations",
                schema: "community");

            migrationBuilder.DropTable(
                name: "persons",
                schema: "community");

            migrationBuilder.DropTable(
                name: "households",
                schema: "community");

            migrationBuilder.DropTable(
                name: "meetings",
                schema: "community");

            migrationBuilder.DropTable(
                name: "memberships",
                schema: "community");
        }
    }
}
