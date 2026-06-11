using Elovo.Infrastructure.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Elovo.Infrastructure.Migrations;

[DbContext(typeof(ElovoDbContext))]
[Migration("20260612000000_AddActivityVisibilityToUserSessions")]
public partial class AddActivityVisibilityToUserSessions : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "ActivityVisibility",
            table: "UserSessions",
            type: "character varying(32)",
            maxLength: 32,
            nullable: false,
            defaultValue: "full");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "ActivityVisibility",
            table: "UserSessions");
    }
}
