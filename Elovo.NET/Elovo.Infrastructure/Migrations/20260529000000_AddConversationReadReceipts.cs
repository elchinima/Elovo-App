using Elovo.Infrastructure.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Elovo.Infrastructure.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(ElovoDbContext))]
    [Migration("20260529000000_AddConversationReadReceipts")]
    public partial class AddConversationReadReceipts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "FirstUserReadAt",
                table: "Conversations",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "SecondUserReadAt",
                table: "Conversations",
                type: "timestamp with time zone",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FirstUserReadAt",
                table: "Conversations");

            migrationBuilder.DropColumn(
                name: "SecondUserReadAt",
                table: "Conversations");
        }
    }
}
