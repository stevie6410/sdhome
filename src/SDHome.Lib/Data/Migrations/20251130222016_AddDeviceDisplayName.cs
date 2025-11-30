using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SDHome.Lib.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddDeviceDisplayName : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DisplayName",
                table: "devices",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "DisplayName",
                table: "devices");
        }
    }
}
