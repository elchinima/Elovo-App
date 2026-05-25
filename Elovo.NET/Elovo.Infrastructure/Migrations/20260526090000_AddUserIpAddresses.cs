using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Elovo.Infrastructure.Data;

#nullable disable

namespace Elovo.Infrastructure.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(ElovoDbContext))]
    [Migration("20260526090000_AddUserIpAddresses")]
    public partial class AddUserIpAddresses : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "LastLoginIp",
                table: "Users",
                type: "character varying(45)",
                maxLength: 45,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RegistrationIp",
                table: "Users",
                type: "character varying(45)",
                maxLength: 45,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LastLoginIp",
                table: "Users");

            migrationBuilder.DropColumn(
                name: "RegistrationIp",
                table: "Users");
        }
    }
}
