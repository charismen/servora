using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Servora.Api.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddDispatchSchedulingAndServiceResources : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "dispatch_notes",
                table: "service_tickets",
                type: "varchar(1000)",
                maxLength: 1000,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<string>(
                name: "dispatch_status",
                table: "service_tickets",
                type: "varchar(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "Unscheduled")
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<int>(
                name: "service_resource_id",
                table: "service_tickets",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "visit_completed_at",
                table: "service_tickets",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "visit_started_at",
                table: "service_tickets",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "service_resources",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    workspace_id = table.Column<int>(type: "int", nullable: false),
                    name = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    code = table.Column<string>(type: "varchar(30)", maxLength: 30, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    resource_type = table.Column<string>(type: "varchar(30)", maxLength: 30, nullable: false, defaultValue: "Bay")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    color = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false, defaultValue: "#b45039")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    notes = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    sort_order = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    is_active = table.Column<bool>(type: "tinyint(1)", nullable: false, defaultValue: true),
                    created_at = table.Column<DateTimeOffset>(type: "datetime(6)", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_service_resources", x => x.id);
                    table.ForeignKey(
                        name: "FK_service_resources_workspaces_workspace_id",
                        column: x => x.workspace_id,
                        principalTable: "workspaces",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_service_tickets_service_resource_id",
                table: "service_tickets",
                column: "service_resource_id");

            migrationBuilder.CreateIndex(
                name: "IX_service_tickets_workspace_id_scheduled_visit_start_scheduled~",
                table: "service_tickets",
                columns: new[] { "workspace_id", "scheduled_visit_start", "scheduled_visit_end" });

            migrationBuilder.CreateIndex(
                name: "IX_service_resources_workspace_id_is_active_sort_order",
                table: "service_resources",
                columns: new[] { "workspace_id", "is_active", "sort_order" });

            migrationBuilder.CreateIndex(
                name: "IX_service_resources_workspace_id_name",
                table: "service_resources",
                columns: new[] { "workspace_id", "name" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_service_tickets_service_resources_service_resource_id",
                table: "service_tickets",
                column: "service_resource_id",
                principalTable: "service_resources",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_service_tickets_service_resources_service_resource_id",
                table: "service_tickets");

            migrationBuilder.DropTable(
                name: "service_resources");

            migrationBuilder.DropIndex(
                name: "IX_service_tickets_service_resource_id",
                table: "service_tickets");

            migrationBuilder.DropIndex(
                name: "IX_service_tickets_workspace_id_scheduled_visit_start_scheduled~",
                table: "service_tickets");

            migrationBuilder.DropColumn(
                name: "dispatch_notes",
                table: "service_tickets");

            migrationBuilder.DropColumn(
                name: "dispatch_status",
                table: "service_tickets");

            migrationBuilder.DropColumn(
                name: "service_resource_id",
                table: "service_tickets");

            migrationBuilder.DropColumn(
                name: "visit_completed_at",
                table: "service_tickets");

            migrationBuilder.DropColumn(
                name: "visit_started_at",
                table: "service_tickets");
        }
    }
}
