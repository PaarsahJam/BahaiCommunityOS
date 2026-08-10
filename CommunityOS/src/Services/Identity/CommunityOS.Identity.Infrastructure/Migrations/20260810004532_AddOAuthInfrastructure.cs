using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CommunityOS.Migrations
{
    /// <inheritdoc />
    public partial class AddOAuthInfrastructure : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "client_id",
                schema: "identity",
                table: "sessions",
                type: "character varying(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "authorization_codes",
                schema: "identity",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    code_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    user_account_id = table.Column<Guid>(type: "uuid", nullable: false),
                    oauth_client_id = table.Column<Guid>(type: "uuid", nullable: false),
                    client_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    redirect_uri = table.Column<string>(type: "character varying(2048)", maxLength: 2048, nullable: false),
                    code_challenge = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: false),
                    code_challenge_method = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    scope = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    nonce = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    issued_on = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    expires_on = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    consumed_on = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_authorization_codes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "oauth_clients",
                schema: "identity",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    client_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    display_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    type_id = table.Column<int>(type: "integer", nullable: false),
                    enabled = table.Column<bool>(type: "boolean", nullable: false),
                    created_on = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    client_secret_hash = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    redirect_uris = table.Column<string[]>(type: "text[]", nullable: false),
                    allowed_grant_types = table.Column<string[]>(type: "text[]", nullable: false),
                    allowed_scopes = table.Column<string[]>(type: "text[]", nullable: false),
                    Version = table.Column<long>(type: "bigint", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_oauth_clients", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_authorization_codes_code_hash",
                schema: "identity",
                table: "authorization_codes",
                column: "code_hash",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_authorization_codes_oauth_client_id",
                schema: "identity",
                table: "authorization_codes",
                column: "oauth_client_id");

            migrationBuilder.CreateIndex(
                name: "IX_authorization_codes_user_account_id",
                schema: "identity",
                table: "authorization_codes",
                column: "user_account_id");

            migrationBuilder.CreateIndex(
                name: "IX_oauth_clients_client_id",
                schema: "identity",
                table: "oauth_clients",
                column: "client_id",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "authorization_codes",
                schema: "identity");

            migrationBuilder.DropTable(
                name: "oauth_clients",
                schema: "identity");

            migrationBuilder.DropColumn(
                name: "client_id",
                schema: "identity",
                table: "sessions");
        }
    }
}
