using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Elovo.Infrastructure.Migrations;

[DbContext(typeof(ElovoDbContext))]
[Migration("20260601000000_AddUserPreferredLanguage")]
public partial class AddUserPreferredLanguage : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.AddColumn<string>(
            name: "PreferredLanguage",
            table: "Users",
            type: "character varying(2)",
            maxLength: 2,
            nullable: true,
            defaultValue: "en");
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.DropColumn(
            name: "PreferredLanguage",
            table: "Users");
    }
}
