using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Marketplace.Warehouse.Migrations
{
    /// <inheritdoc />
    public partial class RestoreStockItemProductIdUniqueIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_StockItems_ProductId",
                table: "StockItems",
                column: "ProductId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_StockItems_ProductId",
                table: "StockItems");
        }
    }
}
