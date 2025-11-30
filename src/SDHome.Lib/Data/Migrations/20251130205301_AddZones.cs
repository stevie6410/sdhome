using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SDHome.Lib.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddZones : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "zone_id",
                table: "devices",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "zones",
                columns: table => new
                {
                    id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    name = table.Column<string>(type: "nvarchar(255)", maxLength: 255, nullable: false),
                    description = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    icon = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    color = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    parent_zone_id = table.Column<int>(type: "int", nullable: true),
                    sort_order = table.Column<int>(type: "int", nullable: false),
                    created_at = table.Column<DateTime>(type: "datetime2", nullable: false),
                    updated_at = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_zones", x => x.id);
                    table.ForeignKey(
                        name: "FK_zones_zones_parent_zone_id",
                        column: x => x.parent_zone_id,
                        principalTable: "zones",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "idx_devices_zone_id",
                table: "devices",
                column: "zone_id");

            migrationBuilder.CreateIndex(
                name: "idx_zones_name",
                table: "zones",
                column: "name");

            migrationBuilder.CreateIndex(
                name: "idx_zones_parent_zone_id",
                table: "zones",
                column: "parent_zone_id");

            migrationBuilder.AddForeignKey(
                name: "FK_devices_zones_zone_id",
                table: "devices",
                column: "zone_id",
                principalTable: "zones",
                principalColumn: "id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_devices_zones_zone_id",
                table: "devices");

            migrationBuilder.DropTable(
                name: "zones");

            migrationBuilder.DropIndex(
                name: "idx_devices_zone_id",
                table: "devices");

            migrationBuilder.DropColumn(
                name: "zone_id",
                table: "devices");
        }
    }
}
