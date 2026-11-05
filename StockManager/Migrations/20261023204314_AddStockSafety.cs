using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StockManager.Migrations
{
    /// <inheritdoc />
    public partial class AddStockSafety : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "Products",
                type: "rowversion",
                rowVersion: true,
                nullable: false,
                defaultValue: new byte[0]);

            migrationBuilder.AddCheckConstraint(
                name: "CK_Products_Quantity_NonNegative",
                table: "Products",
                sql: "[Quantity] >= 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "CK_Products_Quantity_NonNegative",
                table: "Products");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "Products");
        }
    }
}
