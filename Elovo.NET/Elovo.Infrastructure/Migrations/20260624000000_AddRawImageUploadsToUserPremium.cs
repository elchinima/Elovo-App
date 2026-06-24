using Elovo.Infrastructure.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Elovo.Infrastructure.Migrations;

[DbContext(typeof(ElovoDbContext))]
[Migration("20260624000000_AddRawImageUploadsToUserPremium")]
public partial class AddRawImageUploadsToUserPremium : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            ALTER TABLE "UserPremium"
            ADD COLUMN IF NOT EXISTS "IsRawImageUploadsEnabled" boolean NOT NULL DEFAULT FALSE;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            ALTER TABLE "UserPremium"
            DROP COLUMN IF EXISTS "IsRawImageUploadsEnabled";
            """);
    }
}
