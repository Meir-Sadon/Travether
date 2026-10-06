using System;
using Microsoft.EntityFrameworkCore.Migrations;
using NetTopologySuite.Geometries;

#nullable disable

namespace Travether.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class Notifications : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Point>(
                name: "area",
                table: "vacation_cards",
                type: "geography (point, 4326)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "dedupe_key",
                table: "notifications",
                type: "character varying(120)",
                maxLength: 120,
                nullable: true);

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "delivered_at",
                table: "notifications",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "notification_settings",
                columns: table => new
                {
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    requests = table.Column<bool>(type: "boolean", nullable: false),
                    messages = table.Column<bool>(type: "boolean", nullable: false),
                    matches = table.Column<bool>(type: "boolean", nullable: false),
                    reminders = table.Column<bool>(type: "boolean", nullable: false),
                    reviews = table.Column<bool>(type: "boolean", nullable: false),
                    email = table.Column<bool>(type: "boolean", nullable: false),
                    quiet_from = table.Column<TimeOnly>(type: "time without time zone", nullable: true),
                    quiet_to = table.Column<TimeOnly>(type: "time without time zone", nullable: true),
                    time_zone_id = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_notification_settings", x => x.user_id);
                    table.ForeignKey(
                        name: "fk_notification_settings_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_notifications_undelivered",
                table: "notifications",
                column: "created_at",
                filter: "delivered_at IS NULL");

            migrationBuilder.CreateIndex(
                name: "ix_notifications_user_id_dedupe_key",
                table: "notifications",
                columns: new[] { "user_id", "dedupe_key" },
                unique: true,
                filter: "dedupe_key IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "notification_settings");

            migrationBuilder.DropIndex(
                name: "ix_notifications_undelivered",
                table: "notifications");

            migrationBuilder.DropIndex(
                name: "ix_notifications_user_id_dedupe_key",
                table: "notifications");

            migrationBuilder.DropColumn(
                name: "area",
                table: "vacation_cards");

            migrationBuilder.DropColumn(
                name: "dedupe_key",
                table: "notifications");

            migrationBuilder.DropColumn(
                name: "delivered_at",
                table: "notifications");
        }
    }
}
