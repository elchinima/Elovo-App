using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Elovo.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddFcmTokenAndPendingMessageNotificationFlag : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsNotificationSent",
                table: "PendingMessages",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateIndex(
                name: "IX_PendingMessages_IsNotificationSent",
                table: "PendingMessages",
                column: "IsNotificationSent");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_PendingMessages_IsNotificationSent",
                table: "PendingMessages");

            migrationBuilder.DropColumn(
                name: "IsNotificationSent",
                table: "PendingMessages");
        }
    }
}
