using Elovo.Infrastructure.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Elovo.Infrastructure.Migrations;

[DbContext(typeof(ElovoDbContext))]
[Migration("20260622001000_AddPremiumBadgeVisibilityToUserPremium")]
public partial class AddPremiumBadgeVisibilityToUserPremium : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            ALTER TABLE "UserPremium"
            ADD COLUMN IF NOT EXISTS "IsPremiumBadgeVisible" boolean NOT NULL DEFAULT TRUE;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            ALTER TABLE "UserPremium"
            DROP COLUMN IF EXISTS "IsPremiumBadgeVisible";
            """);
    }
}
