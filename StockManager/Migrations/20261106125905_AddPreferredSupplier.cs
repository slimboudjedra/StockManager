using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StockManager.Migrations
{
    /// <inheritdoc />
    public partial class AddPreferredSupplier : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "PreferredSupplierId",
                table: "Products",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Products_PreferredSupplierId",
                table: "Products",
                column: "PreferredSupplierId");

            migrationBuilder.AddForeignKey(
                name: "FK_Products_Suppliers_PreferredSupplierId",
                table: "Products",
                column: "PreferredSupplierId",
                principalTable: "Suppliers",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Products_Suppliers_PreferredSupplierId",
                table: "Products");

            migrationBuilder.DropIndex(
                name: "IX_Products_PreferredSupplierId",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "PreferredSupplierId",
                table: "Products");
        }
    }
}
