using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Slotify.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Add_BusinessContact : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "address",
                table: "businesses",
                type: "character varying(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "phone",
                table: "businesses",
                type: "character varying(40)",
                maxLength: 40,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "address",
                table: "businesses");

            migrationBuilder.DropColumn(
                name: "phone",
                table: "businesses");
        }
    }
}
