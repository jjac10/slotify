using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Slotify.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddGuests : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "guests",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    business_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    phone_encrypted = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    email_encrypted = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: true),
                    phone_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    email_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: true),
                    user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    total_reservations = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    last_reservation_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false, defaultValue: "active"),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_guests", x => x.id);
                    table.CheckConstraint("ck_guests_phone_or_email", "phone_hash IS NOT NULL OR email_hash IS NOT NULL");
                    table.ForeignKey(
                        name: "FK_guests_businesses_business_id",
                        column: x => x.business_id,
                        principalTable: "businesses",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_guests_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_guests_business_id_email_hash",
                table: "guests",
                columns: new[] { "business_id", "email_hash" },
                unique: true,
                filter: "email_hash IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_guests_business_id_phone_hash",
                table: "guests",
                columns: new[] { "business_id", "phone_hash" },
                unique: true,
                filter: "phone_hash IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_guests_user_id",
                table: "guests",
                column: "user_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "guests");
        }
    }
}
