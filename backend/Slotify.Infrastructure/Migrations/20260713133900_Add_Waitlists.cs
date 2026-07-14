using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Slotify.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Add_Waitlists : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "waitlists",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    business_id = table.Column<Guid>(type: "uuid", nullable: false),
                    service_id = table.Column<Guid>(type: "uuid", nullable: false),
                    waitlist_date = table.Column<DateOnly>(type: "date", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    guest_id = table.Column<Guid>(type: "uuid", nullable: true),
                    position = table.Column<int>(type: "integer", nullable: false),
                    status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false, defaultValue: "waiting"),
                    notified_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_waitlists", x => x.id);
                    table.CheckConstraint("waitlist_user_or_guest", "(user_id IS NOT NULL AND guest_id IS NULL) OR (user_id IS NULL AND guest_id IS NOT NULL)");
                    table.ForeignKey(
                        name: "FK_waitlists_businesses_business_id",
                        column: x => x.business_id,
                        principalTable: "businesses",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_waitlists_guests_guest_id",
                        column: x => x.guest_id,
                        principalTable: "guests",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_waitlists_services_service_id",
                        column: x => x.service_id,
                        principalTable: "services",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_waitlists_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_waitlists_business_id",
                table: "waitlists",
                column: "business_id");

            migrationBuilder.CreateIndex(
                name: "IX_waitlists_guest_id",
                table: "waitlists",
                column: "guest_id");

            migrationBuilder.CreateIndex(
                name: "IX_waitlists_service_id_waitlist_date_status_position",
                table: "waitlists",
                columns: new[] { "service_id", "waitlist_date", "status", "position" });

            migrationBuilder.CreateIndex(
                name: "IX_waitlists_service_id_waitlist_date_user_id",
                table: "waitlists",
                columns: new[] { "service_id", "waitlist_date", "user_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_waitlists_user_id",
                table: "waitlists",
                column: "user_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "waitlists");
        }
    }
}
