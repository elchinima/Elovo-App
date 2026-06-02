using Elovo.Infrastructure.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Elovo.Infrastructure.Migrations;

[DbContext(typeof(ElovoDbContext))]
[Migration("20260602000000_MovePreferredLanguageToUserSessions")]
public partial class MovePreferredLanguageToUserSessions : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "PreferredLanguage",
            table: "UserSessions",
            type: "character varying(2)",
            maxLength: 2,
            nullable: true,
            defaultValue: "en");

        migrationBuilder.Sql("""
            INSERT INTO "UserSessions" ("UserId", "PreferredLanguage")
            SELECT "Id", "PreferredLanguage"
            FROM "Users"
            ON CONFLICT ("UserId") DO NOTHING;
            """);

        migrationBuilder.Sql("""
            UPDATE "UserSessions" AS sessions
            SET "PreferredLanguage" = users."PreferredLanguage"
            FROM "Users" AS users
            WHERE sessions."UserId" = users."Id";
            """);

        migrationBuilder.DropColumn(
            name: "PreferredLanguage",
            table: "Users");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "PreferredLanguage",
            table: "Users",
            type: "character varying(2)",
            maxLength: 2,
            nullable: true,
            defaultValue: "en");

        migrationBuilder.Sql("""
            UPDATE "Users" AS users
            SET "PreferredLanguage" = sessions."PreferredLanguage"
            FROM "UserSessions" AS sessions
            WHERE users."Id" = sessions."UserId";
            """);

        migrationBuilder.DropColumn(
            name: "PreferredLanguage",
            table: "UserSessions");
    }
}
