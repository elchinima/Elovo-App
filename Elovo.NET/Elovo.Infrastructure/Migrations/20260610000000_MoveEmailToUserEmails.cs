using System;
using Elovo.Infrastructure.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Elovo.Infrastructure.Migrations;

[DbContext(typeof(ElovoDbContext))]
[Migration("20260610000000_MoveEmailToUserEmails")]
public partial class MoveEmailToUserEmails : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "UserEmails",
            columns: table => new
            {
                UserId = table.Column<Guid>(type: "uuid", nullable: false),
                Email = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                IsEmailConfirmed = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                EmailConfirmationCodeHash = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                EmailConfirmationCodeExpiredAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                LastEmailSentAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
            },
            constraints: table =>
            {
                table.PrimaryKey("PK_UserEmails", x => x.UserId);
                table.ForeignKey(
                    name: "FK_UserEmails_Users_UserId",
                    column: x => x.UserId,
                    principalTable: "Users",
                    principalColumn: "Id",
                    onDelete: ReferentialAction.Cascade);
            });

        migrationBuilder.Sql("""
            INSERT INTO "UserEmails" ("UserId", "Email", "IsEmailConfirmed", "EmailConfirmationCodeHash", "EmailConfirmationCodeExpiredAt", "LastEmailSentAt")
            SELECT "Id", "Email", "IsEmailConfirmed", "EmailConfirmationCodeHash", "EmailConfirmationCodeExpiredAt", "LastEmailSentAt"
            FROM "Users";
            """);

        migrationBuilder.DropIndex(
            name: "IX_Users_Email",
            table: "Users");

        migrationBuilder.DropColumn(
            name: "Email",
            table: "Users");

        migrationBuilder.DropColumn(
            name: "EmailConfirmationCodeHash",
            table: "Users");

        migrationBuilder.DropColumn(
            name: "EmailConfirmationCodeExpiredAt",
            table: "Users");

        migrationBuilder.DropColumn(
            name: "IsEmailConfirmed",
            table: "Users");

        migrationBuilder.DropColumn(
            name: "LastEmailSentAt",
            table: "Users");

        migrationBuilder.CreateIndex(
            name: "IX_UserEmails_Email",
            table: "UserEmails",
            column: "Email",
            unique: true,
            filter: "\"Email\" IS NOT NULL");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "Email",
            table: "Users",
            type: "character varying(256)",
            maxLength: 256,
            nullable: true);

        migrationBuilder.AddColumn<string>(
            name: "EmailConfirmationCodeHash",
            table: "Users",
            type: "character varying(256)",
            maxLength: 256,
            nullable: true);

        migrationBuilder.AddColumn<DateTime>(
            name: "EmailConfirmationCodeExpiredAt",
            table: "Users",
            type: "timestamp with time zone",
            nullable: true);

        migrationBuilder.AddColumn<bool>(
            name: "IsEmailConfirmed",
            table: "Users",
            type: "boolean",
            nullable: false,
            defaultValue: false);

        migrationBuilder.AddColumn<DateTime>(
            name: "LastEmailSentAt",
            table: "Users",
            type: "timestamp with time zone",
            nullable: true);

        migrationBuilder.Sql("""
            UPDATE "Users" AS u
            SET
                "Email" = e."Email",
                "IsEmailConfirmed" = e."IsEmailConfirmed",
                "EmailConfirmationCodeHash" = e."EmailConfirmationCodeHash",
                "EmailConfirmationCodeExpiredAt" = e."EmailConfirmationCodeExpiredAt",
                "LastEmailSentAt" = e."LastEmailSentAt"
            FROM "UserEmails" AS e
            WHERE u."Id" = e."UserId";
            """);

        migrationBuilder.DropTable(
            name: "UserEmails");

        migrationBuilder.CreateIndex(
            name: "IX_Users_Email",
            table: "Users",
            column: "Email",
            unique: true,
            filter: "\"Email\" IS NOT NULL");
    }
}
