using StockManager.Models;

namespace StockManager.ViewModels
{
    public class CategoryDetailsViewModel
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public int ActiveProductCount { get; set; }
        public int ArchivedProductCount { get; set; }
        public List<Product> Products { get; set; } = new();
    }
}