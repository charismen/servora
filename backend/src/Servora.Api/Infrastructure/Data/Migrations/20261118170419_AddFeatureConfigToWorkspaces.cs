using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Servora.Api.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddFeatureConfigToWorkspaces : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "feature_config",
                table: "workspaces",
                type: "text",
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "feature_config",
                table: "workspaces");
        }
    }
}
