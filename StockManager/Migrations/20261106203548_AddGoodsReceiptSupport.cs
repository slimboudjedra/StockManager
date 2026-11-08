using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StockManager.Migrations
{
    /// <inheritdoc />
    public partial class AddGoodsReceiptSupport : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "PurchaseOrderLineId",
                table: "StockMovements",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastReceiptAt",
                table: "PurchaseOrders",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "PurchaseOrders",
                type: "rowversion",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.CreateIndex(
                name: "IX_StockMovements_PurchaseOrderLineId",
                table: "StockMovements",
                column: "PurchaseOrderLineId");

            migrationBuilder.AddForeignKey(
                name: "FK_StockMovements_PurchaseOrderLines_PurchaseOrderLineId",
                table: "StockMovements",
                column: "PurchaseOrderLineId",
                principalTable: "PurchaseOrderLines",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_StockMovements_PurchaseOrderLines_PurchaseOrderLineId",
                table: "StockMovements");

            migrationBuilder.DropIndex(
                name: "IX_StockMovements_PurchaseOrderLineId",
                table: "StockMovements");

            migrationBuilder.DropColumn(
                name: "PurchaseOrderLineId",
                table: "StockMovements");

            migrationBuilder.DropColumn(
                name: "LastReceiptAt",
                table: "PurchaseOrders");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "PurchaseOrders");
        }
    }
}
