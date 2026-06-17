using Elovo.Infrastructure.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Elovo.Infrastructure.Migrations;

[DbContext(typeof(ElovoDbContext))]
[Migration("20260618001000_RemovePremiumColumnFromUsers")]
public partial class RemovePremiumColumnFromUsers : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            CREATE TABLE IF NOT EXISTS "UserPremium" (
                "UserId" uuid NOT NULL,
                CONSTRAINT "PK_UserPremium" PRIMARY KEY ("UserId"),
                CONSTRAINT "FK_UserPremium_Users_UserId" FOREIGN KEY ("UserId") REFERENCES "Users" ("Id") ON DELETE CASCADE
            );
            """);

        migrationBuilder.Sql("""
            ALTER TABLE "Users"
            DROP COLUMN IF EXISTS "Premium";
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            ALTER TABLE "Users"
            ADD COLUMN IF NOT EXISTS "Premium" boolean NOT NULL DEFAULT FALSE;
            """);
    }
}
