using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Travether.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class Chat : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "kind",
                table: "messages",
                type: "character varying(24)",
                maxLength: 24,
                nullable: false,
                defaultValue: "text");

            migrationBuilder.CreateTable(
                name: "conversation_reads",
                columns: table => new
                {
                    conversation_id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    last_read_at = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_conversation_reads", x => new { x.conversation_id, x.user_id });
                    table.ForeignKey(
                        name: "fk_conversation_reads_conversations_conversation_id",
                        column: x => x.conversation_id,
                        principalTable: "conversations",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "fk_conversation_reads_users_user_id",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.AddCheckConstraint(
                name: "ck_messages_kind",
                table: "messages",
                sql: "kind IN ('text', 'contact_phone', 'contact_whatsapp')");

            migrationBuilder.CreateIndex(
                name: "ix_conversation_reads_user_id",
                table: "conversation_reads",
                column: "user_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "conversation_reads");

            migrationBuilder.DropCheckConstraint(
                name: "ck_messages_kind",
                table: "messages");

            migrationBuilder.DropColumn(
                name: "kind",
                table: "messages");
        }
    }
}
