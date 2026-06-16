using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Slotify.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddReservations : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "reservations",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    business_id = table.Column<Guid>(type: "uuid", nullable: false),
                    service_id = table.Column<Guid>(type: "uuid", nullable: false),
                    staff_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    guest_id = table.Column<Guid>(type: "uuid", nullable: true),
                    start_time = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    end_time = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false, defaultValue: "pending"),
                    payment_status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false, defaultValue: "not_required"),
                    version = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    cancelled_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_reservations", x => x.id);
                    table.CheckConstraint("ck_reservations_times", "start_time < end_time");
                    table.CheckConstraint("ck_reservations_user_or_guest", "(user_id IS NOT NULL AND guest_id IS NULL) OR (user_id IS NULL AND guest_id IS NOT NULL)");
                    table.ForeignKey(
                        name: "FK_reservations_businesses_business_id",
                        column: x => x.business_id,
                        principalTable: "businesses",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_reservations_guests_guest_id",
                        column: x => x.guest_id,
                        principalTable: "guests",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_reservations_services_service_id",
                        column: x => x.service_id,
                        principalTable: "services",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_reservations_staff_staff_id",
                        column: x => x.staff_id,
                        principalTable: "staff",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_reservations_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_reservations_business_id_start_time",
                table: "reservations",
                columns: new[] { "business_id", "start_time" });

            migrationBuilder.CreateIndex(
                name: "IX_reservations_guest_id_start_time",
                table: "reservations",
                columns: new[] { "guest_id", "start_time" });

            migrationBuilder.CreateIndex(
                name: "IX_reservations_service_id",
                table: "reservations",
                column: "service_id");

            migrationBuilder.CreateIndex(
                name: "IX_reservations_staff_id_start_time",
                table: "reservations",
                columns: new[] { "staff_id", "start_time" });

            migrationBuilder.CreateIndex(
                name: "IX_reservations_status_created_at",
                table: "reservations",
                columns: new[] { "status", "created_at" });

            migrationBuilder.CreateIndex(
                name: "IX_reservations_user_id",
                table: "reservations",
                column: "user_id");

            // Anti-doble-booking robusto (ADR #4, opción B): exclusion constraint que
            // impide a nivel BD cualquier solapamiento de horas del MISMO staff
            // (salvo canceladas), incluso bajo concurrencia. Requiere btree_gist.
            migrationBuilder.Sql("CREATE EXTENSION IF NOT EXISTS btree_gist;");
            migrationBuilder.Sql(
                "ALTER TABLE reservations ADD CONSTRAINT ex_reservations_no_overlap " +
                "EXCLUDE USING gist (staff_id WITH =, tstzrange(start_time, end_time) WITH &&) " +
                "WHERE (status <> 'cancelled');");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "reservations");
        }
    }
}
