using System;
using Elovo.Infrastructure.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Elovo.Infrastructure.Migrations;

[DbContext(typeof(ElovoDbContext))]
[Migration("20260609000000_AddEmailConfirmation")]
public partial class AddEmailConfirmation : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
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
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
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
    }
}
