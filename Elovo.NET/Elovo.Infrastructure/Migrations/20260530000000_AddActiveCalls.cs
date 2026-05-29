using Elovo.Infrastructure.Data;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Elovo.Infrastructure.Migrations
{
    /// <inheritdoc />
    [DbContext(typeof(ElovoDbContext))]
    [Migration("20260530000000_AddActiveCalls")]
    public partial class AddActiveCalls : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ActiveCalls",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    CallerId = table.Column<Guid>(type: "uuid", nullable: false),
                    ReceiverId = table.Column<Guid>(type: "uuid", nullable: false),
                    CallerName = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    CallerAvatar = table.Column<string>(type: "character varying(512)", maxLength: 512, nullable: false),
                    OfferSdp = table.Column<string>(type: "text", nullable: true),
                    StartedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ActiveCalls", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ActiveCalls_Users_CallerId",
                        column: x => x.CallerId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ActiveCalls_Users_ReceiverId",
                        column: x => x.ReceiverId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ActiveCalls_ReceiverId",
                table: "ActiveCalls",
                column: "ReceiverId");

            migrationBuilder.CreateIndex(
                name: "IX_ActiveCalls_StartedAt",
                table: "ActiveCalls",
                column: "StartedAt");

            migrationBuilder.CreateIndex(
                name: "IX_ActiveCalls_CallerId_ReceiverId",
                table: "ActiveCalls",
                columns: new[] { "CallerId", "ReceiverId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ActiveCalls");
        }
    }
}
