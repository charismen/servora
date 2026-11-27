using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Servora.Api.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddCompensationCatalogPayPolicyAndWorkItemBonusTracking : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "AssignedTechnicianUserId",
                table: "service_ticket_work_items",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BonusTaskKey",
                table: "service_ticket_work_items",
                type: "varchar(60)",
                maxLength: 60,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "CompletedAt",
                table: "service_ticket_work_items",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "CompletedByUserId",
                table: "service_ticket_work_items",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DeviceCategoryOverride",
                table: "service_ticket_work_items",
                type: "varchar(20)",
                maxLength: 20,
                nullable: true)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.AddColumn<bool>(
                name: "IsInternalOnly",
                table: "service_ticket_work_items",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "CompensationTemplateId",
                table: "freelance_profiles",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PayPolicyJson",
                table: "freelance_profiles",
                type: "json",
                nullable: false)
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "bonus_task_definitions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    workspace_id = table.Column<int>(type: "int", nullable: false),
                    TaskKey = table.Column<string>(type: "varchar(60)", maxLength: 60, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Label = table.Column<string>(type: "varchar(150)", maxLength: 150, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Description = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Category = table.Column<string>(type: "varchar(60)", maxLength: 60, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    DefaultStandardRate = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    DefaultGamingRate = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    DefaultCustomerPrice = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    DefaultWorkItemType = table.Column<string>(type: "varchar(30)", maxLength: 30, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    DefaultIsInternalOnly = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    LegacyKeywords = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    IsActive = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetime(6)", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_bonus_task_definitions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_bonus_task_definitions_workspaces_workspace_id",
                        column: x => x.workspace_id,
                        principalTable: "workspaces",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "compensation_templates",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    workspace_id = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "varchar(120)", maxLength: 120, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    Description = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    BasePayMode = table.Column<string>(type: "varchar(30)", maxLength: 30, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    BaseHourlyRate = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    TieredRatesJson = table.Column<string>(type: "json", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    PayPolicyJson = table.Column<string>(type: "json", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    BonusConfigsJson = table.Column<string>(type: "json", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    IsActive = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "datetime(6)", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_compensation_templates", x => x.Id);
                    table.ForeignKey(
                        name: "FK_compensation_templates_workspaces_workspace_id",
                        column: x => x.workspace_id,
                        principalTable: "workspaces",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_service_ticket_work_items_AssignedTechnicianUserId",
                table: "service_ticket_work_items",
                column: "AssignedTechnicianUserId");

            migrationBuilder.CreateIndex(
                name: "IX_service_ticket_work_items_CompletedByUserId",
                table: "service_ticket_work_items",
                column: "CompletedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_service_ticket_work_items_workspace_id_BonusTaskKey_IsComple~",
                table: "service_ticket_work_items",
                columns: new[] { "workspace_id", "BonusTaskKey", "IsCompleted" });

            migrationBuilder.CreateIndex(
                name: "IX_freelance_profiles_CompensationTemplateId",
                table: "freelance_profiles",
                column: "CompensationTemplateId");

            migrationBuilder.CreateIndex(
                name: "IX_bonus_task_definitions_workspace_id_IsActive_SortOrder",
                table: "bonus_task_definitions",
                columns: new[] { "workspace_id", "IsActive", "SortOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_bonus_task_definitions_workspace_id_TaskKey",
                table: "bonus_task_definitions",
                columns: new[] { "workspace_id", "TaskKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_compensation_templates_workspace_id_IsActive_SortOrder",
                table: "compensation_templates",
                columns: new[] { "workspace_id", "IsActive", "SortOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_compensation_templates_workspace_id_Name",
                table: "compensation_templates",
                columns: new[] { "workspace_id", "Name" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_freelance_profiles_compensation_templates_CompensationTempla~",
                table: "freelance_profiles",
                column: "CompensationTemplateId",
                principalTable: "compensation_templates",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_service_ticket_work_items_users_AssignedTechnicianUserId",
                table: "service_ticket_work_items",
                column: "AssignedTechnicianUserId",
                principalTable: "users",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_service_ticket_work_items_users_CompletedByUserId",
                table: "service_ticket_work_items",
                column: "CompletedByUserId",
                principalTable: "users",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_freelance_profiles_compensation_templates_CompensationTempla~",
                table: "freelance_profiles");

            migrationBuilder.DropForeignKey(
                name: "FK_service_ticket_work_items_users_AssignedTechnicianUserId",
                table: "service_ticket_work_items");

            migrationBuilder.DropForeignKey(
                name: "FK_service_ticket_work_items_users_CompletedByUserId",
                table: "service_ticket_work_items");

            migrationBuilder.DropTable(
                name: "bonus_task_definitions");

            migrationBuilder.DropTable(
                name: "compensation_templates");

            migrationBuilder.DropIndex(
                name: "IX_service_ticket_work_items_AssignedTechnicianUserId",
                table: "service_ticket_work_items");

            migrationBuilder.DropIndex(
                name: "IX_service_ticket_work_items_CompletedByUserId",
                table: "service_ticket_work_items");

            migrationBuilder.DropIndex(
                name: "IX_service_ticket_work_items_workspace_id_BonusTaskKey_IsComple~",
                table: "service_ticket_work_items");

            migrationBuilder.DropIndex(
                name: "IX_freelance_profiles_CompensationTemplateId",
                table: "freelance_profiles");

            migrationBuilder.DropColumn(
                name: "AssignedTechnicianUserId",
                table: "service_ticket_work_items");

            migrationBuilder.DropColumn(
                name: "BonusTaskKey",
                table: "service_ticket_work_items");

            migrationBuilder.DropColumn(
                name: "CompletedAt",
                table: "service_ticket_work_items");

            migrationBuilder.DropColumn(
                name: "CompletedByUserId",
                table: "service_ticket_work_items");

            migrationBuilder.DropColumn(
                name: "DeviceCategoryOverride",
                table: "service_ticket_work_items");

            migrationBuilder.DropColumn(
                name: "IsInternalOnly",
                table: "service_ticket_work_items");

            migrationBuilder.DropColumn(
                name: "CompensationTemplateId",
                table: "freelance_profiles");

            migrationBuilder.DropColumn(
                name: "PayPolicyJson",
                table: "freelance_profiles");
        }
    }
}
