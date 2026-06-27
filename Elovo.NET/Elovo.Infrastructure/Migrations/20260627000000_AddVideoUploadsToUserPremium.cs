using Elovo.Infrastructure.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Elovo.Infrastructure.Migrations;

[DbContext(typeof(ElovoDbContext))]
[Migration("20260627000000_AddVideoUploadsToUserPremium")]
public partial class AddVideoUploadsToUserPremium : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            ALTER TABLE "UserPremium"
            ADD COLUMN IF NOT EXISTS "IsVideoUploadsEnabled" boolean NOT NULL DEFAULT FALSE;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            ALTER TABLE "UserPremium"
            DROP COLUMN IF EXISTS "IsVideoUploadsEnabled";
            """);
    }
}
