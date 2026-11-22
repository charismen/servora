using System;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Servora.Api.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddNotificationsWebhooksAndTwoFactorAuth : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "preferred_two_factor_method",
                table: "users",
                type: "varchar(30)",
                maxLength: 30,
                nullable: false,
                defaultValue: "None")
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "otp_verification_challenges",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    workspace_id = table.Column<int>(type: "int", nullable: true),
                    user_id = table.Column<int>(type: "int", nullable: true),
                    service_ticket_id = table.Column<int>(type: "int", nullable: true),
                    purpose = table.Column<string>(type: "varchar(40)", maxLength: 40, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    channel = table.Column<string>(type: "varchar(30)", maxLength: 30, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    challenge_token = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    code_hash = table.Column<string>(type: "varchar(128)", maxLength: 128, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    code_salt = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    masked_destination = table.Column<string>(type: "varchar(120)", maxLength: 120, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    attempt_count = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    max_attempts = table.Column<int>(type: "int", nullable: false, defaultValue: 5),
                    expires_at = table.Column<DateTimeOffset>(type: "datetime(6)", nullable: false),
                    consumed_at = table.Column<DateTimeOffset>(type: "datetime(6)", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_otp_verification_challenges", x => x.id);
                    table.ForeignKey(
                        name: "FK_otp_verification_challenges_service_tickets_service_ticket_id",
                        column: x => x.service_ticket_id,
                        principalTable: "service_tickets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_otp_verification_challenges_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_otp_verification_challenges_workspaces_workspace_id",
                        column: x => x.workspace_id,
                        principalTable: "workspaces",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "workspace_notification_channels",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    workspace_id = table.Column<int>(type: "int", nullable: false),
                    email_enabled = table.Column<bool>(type: "tinyint(1)", nullable: false, defaultValue: false),
                    use_system_smtp = table.Column<bool>(type: "tinyint(1)", nullable: false, defaultValue: true),
                    smtp_host = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    smtp_port = table.Column<int>(type: "int", nullable: false, defaultValue: 587),
                    smtp_use_ssl = table.Column<bool>(type: "tinyint(1)", nullable: false, defaultValue: true),
                    smtp_username = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    smtp_password_encrypted = table.Column<string>(type: "text", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    smtp_password_last4 = table.Column<string>(type: "varchar(8)", maxLength: 8, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    from_email = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    from_name = table.Column<string>(type: "varchar(150)", maxLength: 150, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    staff_notification_emails = table.Column<string>(type: "varchar(1000)", maxLength: 1000, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    whatsapp_enabled = table.Column<bool>(type: "tinyint(1)", nullable: false, defaultValue: false),
                    whatsapp_provider = table.Column<string>(type: "varchar(30)", maxLength: 30, nullable: false, defaultValue: "Manual")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    whatsapp_api_url = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    whatsapp_phone_number_id = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    whatsapp_api_token_encrypted = table.Column<string>(type: "text", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    whatsapp_api_token_last4 = table.Column<string>(type: "varchar(8)", maxLength: 8, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    whatsapp_auth_header_name = table.Column<string>(type: "varchar(80)", maxLength: 80, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    whatsapp_custom_payload_template = table.Column<string>(type: "text", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    telegram_enabled = table.Column<bool>(type: "tinyint(1)", nullable: false, defaultValue: false),
                    telegram_bot_token_encrypted = table.Column<string>(type: "text", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    telegram_bot_token_last4 = table.Column<string>(type: "varchar(8)", maxLength: 8, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    telegram_chat_id = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    telegram_thread_id = table.Column<int>(type: "int", nullable: true),
                    two_factor_enforcement = table.Column<string>(type: "varchar(30)", maxLength: 30, nullable: false, defaultValue: "Optional")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    require_otp_for_public_quote_approval = table.Column<bool>(type: "tinyint(1)", nullable: false, defaultValue: false),
                    created_at = table.Column<DateTimeOffset>(type: "datetime(6)", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_workspace_notification_channels", x => x.id);
                    table.ForeignKey(
                        name: "FK_workspace_notification_channels_workspaces_workspace_id",
                        column: x => x.workspace_id,
                        principalTable: "workspaces",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "workspace_notification_rules",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    workspace_id = table.Column<int>(type: "int", nullable: false),
                    event_type = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    send_customer_email = table.Column<bool>(type: "tinyint(1)", nullable: false, defaultValue: false),
                    send_customer_whatsapp = table.Column<bool>(type: "tinyint(1)", nullable: false, defaultValue: false),
                    send_staff_email = table.Column<bool>(type: "tinyint(1)", nullable: false, defaultValue: false),
                    send_staff_telegram = table.Column<bool>(type: "tinyint(1)", nullable: false, defaultValue: false),
                    send_webhook = table.Column<bool>(type: "tinyint(1)", nullable: false, defaultValue: true),
                    created_at = table.Column<DateTimeOffset>(type: "datetime(6)", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_workspace_notification_rules", x => x.id);
                    table.ForeignKey(
                        name: "FK_workspace_notification_rules_workspaces_workspace_id",
                        column: x => x.workspace_id,
                        principalTable: "workspaces",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "workspace_webhook_endpoints",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    workspace_id = table.Column<int>(type: "int", nullable: false),
                    name = table.Column<string>(type: "varchar(120)", maxLength: 120, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    target_url = table.Column<string>(type: "varchar(1000)", maxLength: 1000, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    is_active = table.Column<bool>(type: "tinyint(1)", nullable: false, defaultValue: true),
                    signing_secret_encrypted = table.Column<string>(type: "text", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    signing_secret_last4 = table.Column<string>(type: "varchar(8)", maxLength: 8, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    subscribed_events_json = table.Column<string>(type: "text", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    custom_headers_json = table.Column<string>(type: "text", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    last_triggered_at = table.Column<DateTimeOffset>(type: "datetime(6)", nullable: true),
                    last_status_code = table.Column<int>(type: "int", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "datetime(6)", nullable: false),
                    updated_at = table.Column<DateTimeOffset>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_workspace_webhook_endpoints", x => x.id);
                    table.ForeignKey(
                        name: "FK_workspace_webhook_endpoints_workspaces_workspace_id",
                        column: x => x.workspace_id,
                        principalTable: "workspaces",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "notification_deliveries",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySql:ValueGenerationStrategy", MySqlValueGenerationStrategy.IdentityColumn),
                    workspace_id = table.Column<int>(type: "int", nullable: true),
                    channel = table.Column<string>(type: "varchar(30)", maxLength: 30, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    provider = table.Column<string>(type: "varchar(40)", maxLength: 40, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    event_type = table.Column<string>(type: "varchar(60)", maxLength: 60, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    recipient = table.Column<string>(type: "varchar(250)", maxLength: 250, nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    subject = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    payload_preview = table.Column<string>(type: "text", nullable: false)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    raw_payload_json = table.Column<string>(type: "text", nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    status = table.Column<string>(type: "varchar(30)", maxLength: 30, nullable: false, defaultValue: "Pending")
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    http_status_code = table.Column<int>(type: "int", nullable: true),
                    error_message = table.Column<string>(type: "varchar(1000)", maxLength: 1000, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    attempt_count = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    webhook_endpoint_id = table.Column<int>(type: "int", nullable: true),
                    reference_entity_type = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: true)
                        .Annotation("MySql:CharSet", "utf8mb4"),
                    reference_entity_id = table.Column<int>(type: "int", nullable: true),
                    created_at = table.Column<DateTimeOffset>(type: "datetime(6)", nullable: false),
                    sent_at = table.Column<DateTimeOffset>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_notification_deliveries", x => x.id);
                    table.ForeignKey(
                        name: "FK_notification_deliveries_workspace_webhook_endpoints_webhook_~",
                        column: x => x.webhook_endpoint_id,
                        principalTable: "workspace_webhook_endpoints",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_notification_deliveries_workspaces_workspace_id",
                        column: x => x.workspace_id,
                        principalTable: "workspaces",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySql:CharSet", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_notification_deliveries_status_created_at",
                table: "notification_deliveries",
                columns: new[] { "status", "created_at" });

            migrationBuilder.CreateIndex(
                name: "IX_notification_deliveries_webhook_endpoint_id",
                table: "notification_deliveries",
                column: "webhook_endpoint_id");

            migrationBuilder.CreateIndex(
                name: "IX_notification_deliveries_workspace_id_created_at",
                table: "notification_deliveries",
                columns: new[] { "workspace_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "IX_otp_verification_challenges_challenge_token",
                table: "otp_verification_challenges",
                column: "challenge_token",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_otp_verification_challenges_purpose_service_ticket_id_create~",
                table: "otp_verification_challenges",
                columns: new[] { "purpose", "service_ticket_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "IX_otp_verification_challenges_purpose_user_id_created_at",
                table: "otp_verification_challenges",
                columns: new[] { "purpose", "user_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "IX_otp_verification_challenges_service_ticket_id",
                table: "otp_verification_challenges",
                column: "service_ticket_id");

            migrationBuilder.CreateIndex(
                name: "IX_otp_verification_challenges_user_id",
                table: "otp_verification_challenges",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "IX_otp_verification_challenges_workspace_id",
                table: "otp_verification_challenges",
                column: "workspace_id");

            migrationBuilder.CreateIndex(
                name: "IX_workspace_notification_channels_workspace_id",
                table: "workspace_notification_channels",
                column: "workspace_id",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_workspace_notification_rules_workspace_id_event_type",
                table: "workspace_notification_rules",
                columns: new[] { "workspace_id", "event_type" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_workspace_webhook_endpoints_workspace_id_is_active",
                table: "workspace_webhook_endpoints",
                columns: new[] { "workspace_id", "is_active" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "notification_deliveries");

            migrationBuilder.DropTable(
                name: "otp_verification_challenges");

            migrationBuilder.DropTable(
                name: "workspace_notification_channels");

            migrationBuilder.DropTable(
                name: "workspace_notification_rules");

            migrationBuilder.DropTable(
                name: "workspace_webhook_endpoints");

            migrationBuilder.DropColumn(
                name: "preferred_two_factor_method",
                table: "users");
        }
    }
}
