using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Travether.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class Safety : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_reports_reporter_id",
                table: "reports");

            migrationBuilder.AddColumn<DateTimeOffset>(
                name: "hidden_at",
                table: "reviews",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "reason",
                table: "reports",
                type: "character varying(32)",
                maxLength: 32,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(1000)",
                oldMaxLength: 1000);

            migrationBuilder.AddColumn<string>(
                name: "details",
                table: "reports",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "banned_identifiers",
                columns: table => new
                {
                    hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    kind = table.Column<string>(type: "character varying(16)", maxLength: 16, nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    created_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false, defaultValueSql: "now()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_banned_identifiers", x => x.hash);
                    table.CheckConstraint("ck_banned_identifiers_kind", "kind IN ('email', 'phone', 'device')");
                });

            migrationBuilder.CreateTable(
                name: "user_devices",
                columns: table => new
                {
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    device_hash = table.Column<string>(type: "character varying(64)", maxLength: 64, nullable: false),
                    last_seen_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_user_devices", x => new { x.user_id, x.device_hash });
                    table.ForeignKey(
                        name: "fk_user_devices_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_reports_reporter_id_target_type_target_id",
                table: "reports",
                columns: new[] { "reporter_id", "target_type", "target_id" },
                unique: true,
                filter: "status = 'open'");

            migrationBuilder.AddCheckConstraint(
                name: "ck_reports_reason",
                table: "reports",
                sql: "reason IN ('spam', 'harassment', 'inappropriate', 'scam', 'safety', 'fake_profile', 'underage', 'other')");

            migrationBuilder.CreateIndex(
                name: "ix_banned_identifiers_user_id",
                table: "banned_identifiers",
                column: "user_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "banned_identifiers");

            migrationBuilder.DropTable(
                name: "user_devices");

            migrationBuilder.DropIndex(
                name: "ix_reports_reporter_id_target_type_target_id",
                table: "reports");

            migrationBuilder.DropCheckConstraint(
                name: "ck_reports_reason",
                table: "reports");

            migrationBuilder.DropColumn(
                name: "hidden_at",
                table: "reviews");

            migrationBuilder.DropColumn(
                name: "details",
                table: "reports");

            migrationBuilder.AlterColumn<string>(
                name: "reason",
                table: "reports",
                type: "character varying(1000)",
                maxLength: 1000,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "character varying(32)",
                oldMaxLength: 32);

            migrationBuilder.CreateIndex(
                name: "ix_reports_reporter_id",
                table: "reports",
                column: "reporter_id");
        }
    }
}
