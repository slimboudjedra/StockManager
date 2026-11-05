using StockManager.Models;

namespace StockManager.ViewModels
{
    public class DashboardViewModel
    {
        public int ActiveProductCount { get; set; }
        public int CategoryCount { get; set; }
        public int LowStockCount { get; set; }
        public decimal TotalStockValue { get; set; }

        public List<DashboardLowStockRow> LowStockProducts { get; set; } = new();
        public List<DashboardMovementRow> RecentMovements { get; set; } = new();
    }

    public class DashboardLowStockRow
    {
        public int Id { get; set; }
        public string Reference { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public int Quantity { get; set; }
        public int MinimumStock { get; set; }
    }

    public class DashboardMovementRow 
    {
        public int ProductId { get; set; }
        public string ProductReference { get; set; } = string.Empty;
        public string ProductName { get; set; } = string.Empty;
        public StockMovementType MovementType { get; set; }
        public int QuantityChange { get; set; }
        public int QuantityAfter { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}