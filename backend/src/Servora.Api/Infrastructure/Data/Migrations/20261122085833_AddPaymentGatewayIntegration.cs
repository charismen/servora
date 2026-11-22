using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Servora.Api.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddPaymentGatewayIntegration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_payment_records_users_UpdatedByUserId",
                table: "payment_records");

            migrationBuilder.AlterColumn<int>(
                name: "UpdatedByUserId",
                table: "payment_records",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.CreateTable(
                name: "payment_checkout_intents",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    workspace_id = table.Column<int>(type: "int", nullable: false),
                    service_ticket_id = table.Column<int>(type: "int", nullable: false),
                    provider = table.Column<string>(type: "varchar(30)", maxLength: 30, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    environment = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    status = table.Column<string>(type: "varchar(30)", maxLength: 30, nullable: false, defaultValue: "Pending")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    amount = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    currency_code = table.Column<string>(type: "varchar(10)", maxLength: 10, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    external_reference = table.Column<string>(type: "varchar(120)", maxLength: 120, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    provider_checkout_id = table.Column<string>(type: "varchar(160)", maxLength: 160, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    payment_url = table.Column<string>(type: "varchar(1000)", maxLength: 1000, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    qr_payload = table.Column<string>(type: "text", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    expires_at = table.Column<DateTimeOffset>(type: "datetime(6)", nullable: false),
                    paid_at = table.Column<DateTimeOffset>(type: "datetime(6)", nullable: true),
                    provider_payment_method = table.Column<string>(type: "varchar(80)", maxLength: 80, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    settled_payment_record_id = table.Column<int>(type: "int", nullable: true),
                    created_by_user_id = table.Column<int>(type: "int", nullable: true),
                    failure_reason = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    created_at = table.Column<DateTimeOffset>(type: "datetime(6)", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_payment_checkout_intents", x => x.id);
                    table.ForeignKey(
                        name: "FK_payment_checkout_intents_payment_records_settled_payment_rec~",
                        column: x => x.settled_payment_record_id,
                        principalTable: "payment_records",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_payment_checkout_intents_service_tickets_service_ticket_id",
                        column: x => x.service_ticket_id,
                        principalTable: "service_tickets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_payment_checkout_intents_users_created_by_user_id",
                        column: x => x.created_by_user_id,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_payment_checkout_intents_workspaces_workspace_id",
                        column: x => x.workspace_id,
                        principalTable: "workspaces",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "payment_webhook_events",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    workspace_id = table.Column<int>(type: "int", nullable: false),
                    provider = table.Column<string>(type: "varchar(30)", maxLength: 30, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    provider_event_id = table.Column<string>(type: "varchar(160)", maxLength: 160, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    event_type = table.Column<string>(type: "varchar(80)", maxLength: 80, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    processing_status = table.Column<string>(type: "varchar(30)", maxLength: 30, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    error_message = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    received_at = table.Column<DateTimeOffset>(type: "datetime(6)", nullable: false),
                    processed_at = table.Column<DateTimeOffset>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_payment_webhook_events", x => x.id);
                    table.ForeignKey(
                        name: "FK_payment_webhook_events_workspaces_workspace_id",
                        column: x => x.workspace_id,
                        principalTable: "workspaces",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "workspace_payment_gateways",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    workspace_id = table.Column<int>(type: "int", nullable: false),
                    provider = table.Column<string>(type: "varchar(30)", maxLength: 30, nullable: false, defaultValue: "None")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    environment = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false, defaultValue: "Sandbox")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    is_enabled = table.Column<bool>(type: "tinyint(1)", nullable: false, defaultValue: false),
                    api_key_encrypted = table.Column<string>(type: "text", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    api_key_last4 = table.Column<string>(type: "varchar(8)", maxLength: 8, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    webhook_secret_encrypted = table.Column<string>(type: "text", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    webhook_secret_last4 = table.Column<string>(type: "varchar(8)", maxLength: 8, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    webhook_token = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    default_link_expiry_hours = table.Column<int>(type: "int", nullable: false, defaultValue: 24),
                    enable_public_tracking_payment = table.Column<bool>(type: "tinyint(1)", nullable: false, defaultValue: false),
                    require_phone_verification_for_public_payment = table.Column<bool>(type: "tinyint(1)", nullable: false, defaultValue: true),
                    created_at = table.Column<DateTimeOffset>(type: "datetime(6)", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_workspace_payment_gateways", x => x.id);
                    table.ForeignKey(
                        name: "FK_workspace_payment_gateways_workspaces_workspace_id",
                        column: x => x.workspace_id,
                        principalTable: "workspaces",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_payment_checkout_intents_created_by_user_id",
                table: "payment_checkout_intents",
                column: "created_by_user_id");

            migrationBuilder.CreateIndex(
                name: "IX_payment_checkout_intents_service_ticket_id",
                table: "payment_checkout_intents",
                column: "service_ticket_id");

            migrationBuilder.CreateIndex(
                name: "IX_payment_checkout_intents_settled_payment_record_id",
                table: "payment_checkout_intents",
                column: "settled_payment_record_id");

            migrationBuilder.CreateIndex(
                name: "IX_payment_checkout_intents_status_expires_at",
                table: "payment_checkout_intents",
                columns: new[] { "status", "expires_at" });

            migrationBuilder.CreateIndex(
                name: "IX_payment_checkout_intents_workspace_id_external_reference",
                table: "payment_checkout_intents",
                columns: new[] { "workspace_id", "external_reference" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_payment_checkout_intents_workspace_id_provider_checkout_id",
                table: "payment_checkout_intents",
                columns: new[] { "workspace_id", "provider_checkout_id" });

            migrationBuilder.CreateIndex(
                name: "IX_payment_checkout_intents_workspace_id_service_ticket_id_stat~",
                table: "payment_checkout_intents",
                columns: new[] { "workspace_id", "service_ticket_id", "status" });

            migrationBuilder.CreateIndex(
                name: "IX_payment_webhook_events_workspace_id_provider_provider_event_~",
                table: "payment_webhook_events",
                columns: new[] { "workspace_id", "provider", "provider_event_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_workspace_payment_gateways_webhook_token",
                table: "workspace_payment_gateways",
                column: "webhook_token",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_workspace_payment_gateways_workspace_id",
                table: "workspace_payment_gateways",
                column: "workspace_id",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_payment_records_users_UpdatedByUserId",
                table: "payment_records",
                column: "UpdatedByUserId",
                principalTable: "users",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_payment_records_users_UpdatedByUserId",
                table: "payment_records");

            migrationBuilder.DropTable(
                name: "payment_checkout_intents");

            migrationBuilder.DropTable(
                name: "payment_webhook_events");

            migrationBuilder.DropTable(
                name: "workspace_payment_gateways");

            migrationBuilder.AlterColumn<int>(
                name: "UpdatedByUserId",
                table: "payment_records",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.AddForeignKey(
                name: "FK_payment_records_users_UpdatedByUserId",
                table: "payment_records",
                column: "UpdatedByUserId",
                principalTable: "users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
