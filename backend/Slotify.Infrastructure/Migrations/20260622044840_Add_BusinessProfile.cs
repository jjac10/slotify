using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Slotify.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Add_BusinessProfile : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "category",
                table: "businesses",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "latitude",
                table: "businesses",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "longitude",
                table: "businesses",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "photo_url",
                table: "businesses",
                type: "character varying(2048)",
                maxLength: 2048,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "category",
                table: "businesses");

            migrationBuilder.DropColumn(
                name: "latitude",
                table: "businesses");

            migrationBuilder.DropColumn(
                name: "longitude",
                table: "businesses");

            migrationBuilder.DropColumn(
                name: "photo_url",
                table: "businesses");
        }
    }
}
