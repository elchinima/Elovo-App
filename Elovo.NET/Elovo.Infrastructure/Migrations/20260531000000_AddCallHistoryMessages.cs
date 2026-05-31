using Elovo.Infrastructure.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Elovo.Infrastructure.Migrations
{
    [DbContext(typeof(ElovoDbContext))]
    [Migration("20260531000000_AddCallHistoryMessages")]
    public partial class AddCallHistoryMessages : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "AnsweredAt",
                table: "ActiveCalls",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "CallDurationSeconds",
                table: "PendingMessages",
                type: "double precision",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CallStatus",
                table: "PendingMessages",
                type: "character varying(16)",
                maxLength: 16,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsCall",
                table: "PendingMessages",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AnsweredAt",
                table: "ActiveCalls");

            migrationBuilder.DropColumn(
                name: "CallDurationSeconds",
                table: "PendingMessages");

            migrationBuilder.DropColumn(
                name: "CallStatus",
                table: "PendingMessages");

            migrationBuilder.DropColumn(
                name: "IsCall",
                table: "PendingMessages");
        }
    }
}
