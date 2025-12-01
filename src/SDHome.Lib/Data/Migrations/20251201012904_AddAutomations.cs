using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SDHome.Lib.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddAutomations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "automation_rules",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    name = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    icon = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    color = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    is_enabled = table.Column<bool>(type: "bit", nullable: false),
                    trigger_mode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    condition_mode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    cooldown_seconds = table.Column<int>(type: "int", nullable: false),
                    last_triggered_at = table.Column<DateTime>(type: "datetime2", nullable: true),
                    execution_count = table.Column<int>(type: "int", nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    updated_at = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_automation_rules", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "scenes",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    name = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    icon = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    color = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    device_states = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    updated_at = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_scenes", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "automation_actions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    automation_rule_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    action_type = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    device_id = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    property = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    value = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    delay_seconds = table.Column<int>(type: "int", nullable: true),
                    webhook_url = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    webhook_method = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    webhook_body = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    notification_message = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    notification_title = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    scene_id = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    sort_order = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_automation_actions", x => x.id);
                    table.ForeignKey(
                        name: "FK_automation_actions_automation_rules_automation_rule_id",
                        column: x => x.automation_rule_id,
                        principalTable: "automation_rules",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "automation_conditions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    automation_rule_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    condition_type = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    device_id = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    property = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    @operator = table.Column<string>(name: "operator", type: "nvarchar(100)", maxLength: 100, nullable: true),
                    value = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    value2 = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    time_start = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    time_end = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    days_of_week = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    sort_order = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_automation_conditions", x => x.id);
                    table.ForeignKey(
                        name: "FK_automation_conditions_automation_rules_automation_rule_id",
                        column: x => x.automation_rule_id,
                        principalTable: "automation_rules",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "automation_execution_logs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    automation_rule_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    executed_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    status = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    trigger_source = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    action_results = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    duration_ms = table.Column<int>(type: "int", nullable: false),
                    error_message = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_automation_execution_logs", x => x.id);
                    table.ForeignKey(
                        name: "FK_automation_execution_logs_automation_rules_automation_rule_id",
                        column: x => x.automation_rule_id,
                        principalTable: "automation_rules",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "automation_triggers",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    automation_rule_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    trigger_type = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    device_id = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    property = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    @operator = table.Column<string>(name: "operator", type: "nvarchar(100)", maxLength: 100, nullable: true),
                    value = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    time_expression = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    sun_event = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    offset_minutes = table.Column<int>(type: "int", nullable: true),
                    sort_order = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_automation_triggers", x => x.id);
                    table.ForeignKey(
                        name: "FK_automation_triggers_automation_rules_automation_rule_id",
                        column: x => x.automation_rule_id,
                        principalTable: "automation_rules",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "idx_automation_actions_device_id",
                table: "automation_actions",
                column: "device_id");

            migrationBuilder.CreateIndex(
                name: "idx_automation_actions_rule_id",
                table: "automation_actions",
                column: "automation_rule_id");

            migrationBuilder.CreateIndex(
                name: "idx_automation_conditions_rule_id",
                table: "automation_conditions",
                column: "automation_rule_id");

            migrationBuilder.CreateIndex(
                name: "idx_automation_logs_executed",
                table: "automation_execution_logs",
                column: "executed_at",
                descending: new bool[0]);

            migrationBuilder.CreateIndex(
                name: "idx_automation_logs_rule_executed",
                table: "automation_execution_logs",
                columns: new[] { "automation_rule_id", "executed_at" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "idx_automation_logs_status",
                table: "automation_execution_logs",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "idx_automation_rules_enabled",
                table: "automation_rules",
                column: "is_enabled");

            migrationBuilder.CreateIndex(
                name: "idx_automation_rules_name",
                table: "automation_rules",
                column: "name");

            migrationBuilder.CreateIndex(
                name: "idx_automation_triggers_device_id",
                table: "automation_triggers",
                column: "device_id");

            migrationBuilder.CreateIndex(
                name: "idx_automation_triggers_rule_id",
                table: "automation_triggers",
                column: "automation_rule_id");

            migrationBuilder.CreateIndex(
                name: "idx_automation_triggers_type",
                table: "automation_triggers",
                column: "trigger_type");

            migrationBuilder.CreateIndex(
                name: "idx_scenes_name",
                table: "scenes",
                column: "name");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "automation_actions");

            migrationBuilder.DropTable(
                name: "automation_conditions");

            migrationBuilder.DropTable(
                name: "automation_execution_logs");

            migrationBuilder.DropTable(
                name: "automation_triggers");

            migrationBuilder.DropTable(
                name: "scenes");

            migrationBuilder.DropTable(
                name: "automation_rules");
        }
    }
}
