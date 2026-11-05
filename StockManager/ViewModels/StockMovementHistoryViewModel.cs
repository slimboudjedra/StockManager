using StockManager.Models;

namespace StockManager.ViewModels
{
    public class StockMovementHistoryViewModel
    {
        public int ProductId { get; set; }
        public string ProductReference { get; set; } = string.Empty;
        public string ProductName { get; set; } = string.Empty;
        public int CurrentQuantity { get; set; }

        public List<StockMovement> Movements { get; set; } = new();

        public int CurrentPage { get; set; }
        public int TotalPages { get; set; }
        public int TotalMovements { get; set; }
    }
}