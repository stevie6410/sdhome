using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SDHome.Lib.Data.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "devices",
                columns: table => new
                {
                    device_id = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    friendly_name = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    ieee_address = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    model_id = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    manufacturer = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    description = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    power_source = table.Column<bool>(type: "bit", nullable: false),
                    device_type = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    room = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: true),
                    capabilities = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    attributes = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    last_seen = table.Column<DateTime>(type: "datetime2", nullable: true),
                    is_available = table.Column<bool>(type: "bit", nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    updated_at = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_devices", x => x.device_id);
                });

            migrationBuilder.CreateTable(
                name: "sensor_readings",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    signal_event_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    timestamp_utc = table.Column<DateTime>(type: "datetime2(7)", nullable: false),
                    device_id = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    metric = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    value = table.Column<double>(type: "float", nullable: false),
                    unit = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_sensor_readings", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "signal_events",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    timestamp_utc = table.Column<DateTime>(type: "datetime2(7)", nullable: false),
                    source = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    device_id = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    location = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    capability = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    event_type = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    event_sub_type = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    value = table.Column<double>(type: "float", nullable: true),
                    raw_topic = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: false),
                    raw_payload = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    device_kind = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    event_category = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_signal_events", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "trigger_events",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    signal_event_id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    timestamp_utc = table.Column<DateTime>(type: "datetime2(7)", nullable: false),
                    device_id = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    capability = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    trigger_type = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    trigger_sub_type = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    value_bit = table.Column<bool>(type: "bit", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_trigger_events", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "idx_devices_device_type",
                table: "devices",
                column: "device_type");

            migrationBuilder.CreateIndex(
                name: "idx_devices_is_available",
                table: "devices",
                column: "is_available");

            migrationBuilder.CreateIndex(
                name: "idx_devices_room",
                table: "devices",
                column: "room");

            migrationBuilder.CreateIndex(
                name: "ix_sensor_readings_device_metric_ts",
                table: "sensor_readings",
                columns: new[] { "device_id", "metric", "timestamp_utc" },
                descending: new[] { false, false, true });

            migrationBuilder.CreateIndex(
                name: "ix_sensor_readings_metric_ts",
                table: "sensor_readings",
                columns: new[] { "metric", "timestamp_utc" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "ix_signal_events_device_timestamp",
                table: "signal_events",
                columns: new[] { "device_id", "timestamp_utc" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "ix_signal_events_timestamp",
                table: "signal_events",
                column: "timestamp_utc",
                descending: new bool[0]);

            migrationBuilder.CreateIndex(
                name: "ix_trigger_events_device_timestamp",
                table: "trigger_events",
                columns: new[] { "device_id", "timestamp_utc" },
                descending: new[] { false, true });

            migrationBuilder.CreateIndex(
                name: "ix_trigger_events_type_timestamp",
                table: "trigger_events",
                columns: new[] { "trigger_type", "timestamp_utc" },
                descending: new[] { false, true });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "devices");

            migrationBuilder.DropTable(
                name: "sensor_readings");

            migrationBuilder.DropTable(
                name: "signal_events");

            migrationBuilder.DropTable(
                name: "trigger_events");
        }
    }
}
