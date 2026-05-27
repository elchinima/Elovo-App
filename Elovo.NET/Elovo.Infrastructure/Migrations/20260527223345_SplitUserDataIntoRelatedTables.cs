using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Elovo.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class SplitUserDataIntoRelatedTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "UserSessions",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    LastSeenAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsOnline = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    LastLoginIp = table.Column<string>(type: "character varying(45)", maxLength: 45, nullable: true),
                    RegistrationIp = table.Column<string>(type: "character varying(45)", maxLength: 45, nullable: true),
                    FcmToken = table.Column<string>(type: "character varying(4096)", maxLength: 4096, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserSessions", x => x.UserId);
                    table.ForeignKey(
                        name: "FK_UserSessions_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "UserTwoFactor",
                columns: table => new
                {
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    IsTwoFactorEnabled = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    TwoFactorCodeHash = table.Column<string>(type: "character varying(256)", maxLength: 256, nullable: true),
                    TwoFactorCodeExpiredAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserTwoFactor", x => x.UserId);
                    table.ForeignKey(
                        name: "FK_UserTwoFactor_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.Sql("""
                INSERT INTO "UserSessions" ("UserId", "LastSeenAt", "IsOnline", "LastLoginIp", "RegistrationIp", "FcmToken")
                SELECT "Id", "LastSeenAt", "IsOnline", "LastLoginIp", "RegistrationIp", "FcmToken"
                FROM "Users";
                """);

            migrationBuilder.Sql("""
                INSERT INTO "UserTwoFactor" ("UserId", "IsTwoFactorEnabled", "TwoFactorCodeHash", "TwoFactorCodeExpiredAt")
                SELECT "Id", "IsTwoFactorEnabled", "TwoFactorCodeHash", "TwoFactorCodeExpiresAt"
                FROM "Users";
                """);

            migrationBuilder.CreateIndex(
                name: "IX_UserSessions_IsOnline",
                table: "UserSessions",
                column: "IsOnline");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "UserSessions");

            migrationBuilder.DropTable(
                name: "UserTwoFactor");
        }
    }
}
