using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace StockManager.Migrations
{
    /// <inheritdoc />
    public partial class FilterUniqueProductReference : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Products_Reference",
                table: "Products");

            migrationBuilder.CreateIndex(
                name: "IX_Products_Reference",
                table: "Products",
                column: "Reference",
                unique: true,
                filter: "[IsArchived] = 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Products_Reference",
                table: "Products");

            migrationBuilder.CreateIndex(
                name: "IX_Products_Reference",
                table: "Products",
                column: "Reference",
                unique: true);
        }
    }
}
