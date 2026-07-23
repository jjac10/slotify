using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Slotify.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Add_BusinessBranding : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "brand_color",
                table: "businesses",
                type: "character varying(7)",
                maxLength: 7,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "logo_url",
                table: "businesses",
                type: "character varying(2048)",
                maxLength: 2048,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "brand_color",
                table: "businesses");

            migrationBuilder.DropColumn(
                name: "logo_url",
                table: "businesses");
        }
    }
}
