using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CommunityOS.Migrations
{
    /// <inheritdoc />
    public partial class AddSessionRevocationEpoch : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "session_revocation_epoch",
                schema: "identity",
                table: "user_accounts",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "session_revocation_epoch",
                schema: "identity",
                table: "user_accounts");
        }
    }
}
