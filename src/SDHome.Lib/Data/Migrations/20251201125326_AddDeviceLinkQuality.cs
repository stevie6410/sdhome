using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SDHome.Lib.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddDeviceLinkQuality : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "LinkQuality",
                table: "devices",
                type: "int",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LinkQuality",
                table: "devices");
        }
    }
}
