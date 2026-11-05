using StockManager.Models;

namespace StockManager.ViewModels
{
    public class ProductIndexViewModel
    {
        public IEnumerable<Product> Products { get; set; } = new List<Product>();
        public int CurrentPage { get; set; }
        public int TotalPages { get; set; }
        public string? SearchString { get; set; }
        public bool LowStockOnly { get; set; }
        public int LowStockCount { get; set; }
    }
}
