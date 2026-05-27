using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Elovo.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class RemoveOldColumnsFromUser : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Users_IsOnline",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "FcmToken",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "IsOnline",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "IsTwoFactorEnabled",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "LastLoginIp",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "LastSeenAt",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "RegistrationIp",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "TwoFactorCodeExpiresAt",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "TwoFactorCodeHash",
                table: "Users");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "FcmToken",
                table: "Users",
                type: "character varying(4096)",
                maxLength: 4096,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsOnline",
                table: "Users",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsTwoFactorEnabled",
                table: "Users",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "LastLoginIp",
                table: "Users",
                type: "character varying(45)",
                maxLength: 45,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastSeenAt",
                table: "Users",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RegistrationIp",
                table: "Users",
                type: "character varying(45)",
                maxLength: 45,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "TwoFactorCodeExpiresAt",
                table: "Users",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TwoFactorCodeHash",
                table: "Users",
                type: "character varying(256)",
                maxLength: 256,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Users_IsOnline",
                table: "Users",
                column: "IsOnline");
        }
    }
}
