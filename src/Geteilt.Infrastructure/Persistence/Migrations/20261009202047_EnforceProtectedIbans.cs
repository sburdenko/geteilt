using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Geteilt.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class EnforceProtectedIbans : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // NOT VALID, written by hand: the generated AddCheckConstraint checks existing rows, and
            // on a deploy the rows from before encryption are still plain text at this point. NOT
            // VALID enforces the rule on every new write at once — an old version still running
            // can no longer store a plain IBAN — and protect-ibans validates it once the old rows
            // are encrypted.
            migrationBuilder.Sql(
                "ALTER TABLE users ADD CONSTRAINT ck_users_iban_protected CHECK (iban IS NULL OR iban LIKE 'v1:%') NOT VALID;");

            migrationBuilder.Sql(
                "ALTER TABLE members ADD CONSTRAINT ck_members_iban_protected CHECK (iban IS NULL OR iban LIKE 'v1:%') NOT VALID;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_users_iban_protected",
                table: "users");

            migrationBuilder.DropCheckConstraint(
                name: "ck_members_iban_protected",
                table: "members");
        }
    }
}
