using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CommunityOS.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "identity");

            migrationBuilder.CreateTable(
                name: "recovery_requests",
                schema: "identity",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_account_id = table.Column<Guid>(type: "uuid", nullable: false),
                    token_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    purpose = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    requested_on = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    expires_on = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    consumed_on = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    attempts = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_recovery_requests", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "security_events",
                schema: "identity",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_account_id = table.Column<Guid>(type: "uuid", nullable: false),
                    event_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    occurred_on = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ip_address = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    user_agent = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_security_events", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "sessions",
                schema: "identity",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_account_id = table.Column<Guid>(type: "uuid", nullable: false),
                    device_id = table.Column<Guid>(type: "uuid", nullable: false),
                    token_family_id = table.Column<Guid>(type: "uuid", nullable: false),
                    refresh_token_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    created_on = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    expires_on = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    last_used_on = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    revoked_on = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    revocation_reason = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    refresh_token_used = table.Column<bool>(type: "boolean", nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sessions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "user_accounts",
                schema: "identity",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    email = table.Column<string>(type: "character varying(254)", maxLength: 254, nullable: false),
                    status_id = table.Column<int>(type: "integer", nullable: false),
                    created_on = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    verified_on = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    deactivated_on = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    last_login_on = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    failed_login_attempts = table.Column<int>(type: "integer", nullable: false),
                    locked_until = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_accounts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "user_account_credentials",
                schema: "identity",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    type_id = table.Column<int>(type: "integer", nullable: false),
                    secret = table.Column<string>(type: "text", nullable: false),
                    metadata = table.Column<string>(type: "text", nullable: true),
                    created_on = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    last_used_on = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    user_account_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_account_credentials", x => x.id);
                    table.ForeignKey(
                        name: "FK_user_account_credentials_user_accounts_user_account_id",
                        column: x => x.user_account_id,
                        principalSchema: "identity",
                        principalTable: "user_accounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "user_account_devices",
                schema: "identity",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    platform = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    user_agent = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: true),
                    registered_on = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    last_seen_on = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    is_trusted = table.Column<bool>(type: "boolean", nullable: false),
                    trusted_until = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    user_account_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_account_devices", x => x.id);
                    table.ForeignKey(
                        name: "FK_user_account_devices_user_accounts_user_account_id",
                        column: x => x.user_account_id,
                        principalSchema: "identity",
                        principalTable: "user_accounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "user_account_external_identities",
                schema: "identity",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    provider = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    subject = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    linked_on = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    unlinked_on = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    user_account_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_account_external_identities", x => x.id);
                    table.ForeignKey(
                        name: "FK_user_account_external_identities_user_accounts_user_account~",
                        column: x => x.user_account_id,
                        principalSchema: "identity",
                        principalTable: "user_accounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "user_account_mfa_methods",
                schema: "identity",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    type_id = table.Column<int>(type: "integer", nullable: false),
                    secret = table.Column<string>(type: "text", nullable: false),
                    created_on = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    verified_on = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    user_account_id = table.Column<Guid>(type: "uuid", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_account_mfa_methods", x => x.id);
                    table.ForeignKey(
                        name: "FK_user_account_mfa_methods_user_accounts_user_account_id",
                        column: x => x.user_account_id,
                        principalSchema: "identity",
                        principalTable: "user_accounts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_recovery_requests_token_hash",
                schema: "identity",
                table: "recovery_requests",
                column: "token_hash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_recovery_requests_user_account_id_purpose_expires_on",
                schema: "identity",
                table: "recovery_requests",
                columns: new[] { "user_account_id", "purpose", "expires_on" });

            migrationBuilder.CreateIndex(
                name: "IX_security_events_user_account_id_occurred_on",
                schema: "identity",
                table: "security_events",
                columns: new[] { "user_account_id", "occurred_on" });

            migrationBuilder.CreateIndex(
                name: "IX_sessions_refresh_token_hash",
                schema: "identity",
                table: "sessions",
                column: "refresh_token_hash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_sessions_token_family_id",
                schema: "identity",
                table: "sessions",
                column: "token_family_id");

            migrationBuilder.CreateIndex(
                name: "IX_sessions_user_account_id_revoked_on",
                schema: "identity",
                table: "sessions",
                columns: new[] { "user_account_id", "revoked_on" });

            migrationBuilder.CreateIndex(
                name: "IX_user_account_credentials_user_account_id",
                schema: "identity",
                table: "user_account_credentials",
                column: "user_account_id");

            migrationBuilder.CreateIndex(
                name: "IX_user_account_devices_user_account_id",
                schema: "identity",
                table: "user_account_devices",
                column: "user_account_id");

            migrationBuilder.CreateIndex(
                name: "IX_user_account_external_identities_provider_subject",
                schema: "identity",
                table: "user_account_external_identities",
                columns: new[] { "provider", "subject" });

            migrationBuilder.CreateIndex(
                name: "IX_user_account_external_identities_user_account_id",
                schema: "identity",
                table: "user_account_external_identities",
                column: "user_account_id");

            migrationBuilder.CreateIndex(
                name: "IX_user_account_mfa_methods_user_account_id",
                schema: "identity",
                table: "user_account_mfa_methods",
                column: "user_account_id");

            migrationBuilder.CreateIndex(
                name: "IX_user_accounts_email",
                schema: "identity",
                table: "user_accounts",
                column: "email",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "recovery_requests",
                schema: "identity");

            migrationBuilder.DropTable(
                name: "security_events",
                schema: "identity");

            migrationBuilder.DropTable(
                name: "sessions",
                schema: "identity");

            migrationBuilder.DropTable(
                name: "user_account_credentials",
                schema: "identity");

            migrationBuilder.DropTable(
                name: "user_account_devices",
                schema: "identity");

            migrationBuilder.DropTable(
                name: "user_account_external_identities",
                schema: "identity");

            migrationBuilder.DropTable(
                name: "user_account_mfa_methods",
                schema: "identity");

            migrationBuilder.DropTable(
                name: "user_accounts",
                schema: "identity");
        }
    }
}
