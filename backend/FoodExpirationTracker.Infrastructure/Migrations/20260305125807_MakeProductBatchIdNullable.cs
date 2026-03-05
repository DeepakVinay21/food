using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace FoodExpirationTracker.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class MakeProductBatchIdNullable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Notifications_ProductBatchId_NotificationType",
                table: "Notifications");

            migrationBuilder.AlterColumn<Guid>(
                name: "ProductBatchId",
                table: "Notifications",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_ProductBatchId_NotificationType",
                table: "Notifications",
                columns: new[] { "ProductBatchId", "NotificationType" },
                unique: true,
                filter: "\"ProductBatchId\" IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Notifications_ProductBatchId_NotificationType",
                table: "Notifications");

            migrationBuilder.AlterColumn<Guid>(
                name: "ProductBatchId",
                table: "Notifications",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Notifications_ProductBatchId_NotificationType",
                table: "Notifications",
                columns: new[] { "ProductBatchId", "NotificationType" },
                unique: true);
        }
    }
}
