using Elovo.Infrastructure.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Elovo.Infrastructure.Migrations;

[DbContext(typeof(ElovoDbContext))]
[Migration("20260622000000_AddExtendedVoiceMessagesToUserPremium")]
public partial class AddExtendedVoiceMessagesToUserPremium : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            ALTER TABLE "UserPremium"
            ADD COLUMN IF NOT EXISTS "IsExtendedVoiceMessagesEnabled" boolean NOT NULL DEFAULT FALSE;
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            ALTER TABLE "UserPremium"
            DROP COLUMN IF EXISTS "IsExtendedVoiceMessagesEnabled";
            """);
    }
}
