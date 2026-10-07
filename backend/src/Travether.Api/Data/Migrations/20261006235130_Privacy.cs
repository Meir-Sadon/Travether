using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Travether.Api.Data.Migrations
{
    /// <inheritdoc />
    public partial class Privacy : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_login_codes_purpose",
                table: "login_codes");

            migrationBuilder.DropCheckConstraint(
                name: "ck_consents_kind",
                table: "consents");

            migrationBuilder.AddCheckConstraint(
                name: "ck_login_codes_purpose",
                table: "login_codes",
                sql: "purpose IN ('sign_in', 'verify_email', 'reset_password', 'delete_account')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_consents_kind",
                table: "consents",
                sql: "kind IN ('terms', 'privacy_policy', 'community_guidelines', 'marketing_email', 'analytics')");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_login_codes_purpose",
                table: "login_codes");

            migrationBuilder.DropCheckConstraint(
                name: "ck_consents_kind",
                table: "consents");

            migrationBuilder.AddCheckConstraint(
                name: "ck_login_codes_purpose",
                table: "login_codes",
                sql: "purpose IN ('sign_in', 'verify_email', 'reset_password')");

            migrationBuilder.AddCheckConstraint(
                name: "ck_consents_kind",
                table: "consents",
                sql: "kind IN ('terms', 'privacy_policy', 'community_guidelines', 'marketing_email')");
        }
    }
}
