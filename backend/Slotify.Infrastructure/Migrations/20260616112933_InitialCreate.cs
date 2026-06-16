using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Slotify.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "pricing_tiers",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    code = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    max_reservations_per_month = table.Column<int>(type: "integer", nullable: true),
                    max_clients = table.Column<int>(type: "integer", nullable: true),
                    max_services = table.Column<int>(type: "integer", nullable: true),
                    max_staff = table.Column<int>(type: "integer", nullable: true),
                    channel_email = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    channel_sms = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    channel_whatsapp = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    has_analytics = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    has_api = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    price_monthly = table.Column<decimal>(type: "numeric(10,2)", nullable: false, defaultValue: 0m),
                    is_active = table.Column<bool>(type: "boolean", nullable: false, defaultValue: true),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_pricing_tiers", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "users",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    email = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    password_hash = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    phone = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false, defaultValue: "customer"),
                    status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false, defaultValue: "active"),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    deleted_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_users", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "businesses",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false, defaultValueSql: "gen_random_uuid()"),
                    owner_id = table.Column<Guid>(type: "uuid", nullable: false),
                    tier_id = table.Column<Guid>(type: "uuid", nullable: false),
                    name = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false, defaultValue: "active"),
                    created_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()"),
                    updated_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_businesses", x => x.id);
                    table.ForeignKey(
                        name: "FK_businesses_pricing_tiers_tier_id",
                        column: x => x.tier_id,
                        principalTable: "pricing_tiers",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_businesses_users_owner_id",
                        column: x => x.owner_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.InsertData(
                table: "pricing_tiers",
                columns: new[] { "id", "channel_email", "code", "created_at", "is_active", "max_clients", "max_reservations_per_month", "max_services", "max_staff", "name" },
                values: new object[] { new Guid("11111111-1111-1111-1111-111111111111"), true, "free", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), true, 50, 100, 5, 1, "Free" });

            migrationBuilder.InsertData(
                table: "pricing_tiers",
                columns: new[] { "id", "channel_email", "channel_sms", "channel_whatsapp", "code", "created_at", "has_analytics", "has_api", "is_active", "max_clients", "max_reservations_per_month", "max_services", "max_staff", "name", "price_monthly" },
                values: new object[] { new Guid("22222222-2222-2222-2222-222222222222"), true, true, true, "premium", new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), true, true, true, null, null, null, null, "Premium", 9.99m });

            migrationBuilder.CreateIndex(
                name: "IX_businesses_owner_id",
                table: "businesses",
                column: "owner_id");

            migrationBuilder.CreateIndex(
                name: "IX_businesses_tier_id",
                table: "businesses",
                column: "tier_id");

            migrationBuilder.CreateIndex(
                name: "IX_pricing_tiers_code",
                table: "pricing_tiers",
                column: "code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_users_email",
                table: "users",
                column: "email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_users_type_status",
                table: "users",
                columns: new[] { "type", "status" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "businesses");

            migrationBuilder.DropTable(
                name: "pricing_tiers");

            migrationBuilder.DropTable(
                name: "users");
        }
    }
}
