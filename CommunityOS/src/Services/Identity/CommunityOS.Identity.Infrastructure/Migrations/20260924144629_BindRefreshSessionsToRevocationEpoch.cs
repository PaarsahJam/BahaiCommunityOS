using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CommunityOS.Migrations
{
    /// <inheritdoc />
    public partial class BindRefreshSessionsToRevocationEpoch : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "session_revocation_epoch_at_issue",
                schema: "identity",
                table: "sessions",
                type: "bigint",
                nullable: false,
                defaultValue: 0L);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "session_revocation_epoch_at_issue",
                schema: "identity",
                table: "sessions");
        }
    }
}
