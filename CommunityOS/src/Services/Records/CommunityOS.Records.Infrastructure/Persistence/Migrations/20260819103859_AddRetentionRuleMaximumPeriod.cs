using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CommunityOS.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddRetentionRuleMaximumPeriod : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "maximum_period",
                schema: "records",
                table: "retention_rules",
                type: "character varying(30)",
                maxLength: 30,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "maximum_period",
                schema: "records",
                table: "retention_rules");
        }
    }
}
