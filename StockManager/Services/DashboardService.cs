using Microsoft.EntityFrameworkCore;
using StockManager.Data;
using StockManager.ViewModels;

namespace StockManager.Services
{
    public class DashboardService
    {
        private const int ListSize = 5;

        private readonly ApplicationDbContext _context;

        public DashboardService(ApplicationDbContext context)
        {
            _context = context;
        }

        //---------------------------------------------------------------------------------------------------------//

        public async Task<DashboardViewModel> GetDashboardAsync()
        {
            var activeProducts = _context.Products
                .AsNoTracking()
                .Where(p => !p.IsArchived);

            var activeProductCount = await activeProducts.CountAsync();

            var lowStockCount = await activeProducts
                .CountAsync(p => p.Quantity <= p.MinimumStock);

            var categoryCount = await _context.Categories.CountAsync();

        
            var stockLines = await activeProducts
                .Select(p => new { p.Quantity, p.UnitPrice })
                .ToListAsync();

            var totalStockValue = stockLines.Sum(line => line.Quantity * line.UnitPrice);

            var lowStockProducts = await activeProducts
                .Where(p => p.Quantity <= p.MinimumStock)
                .OrderBy(p => p.Quantity - p.MinimumStock)
                .ThenBy(p => p.Name)
                .Take(ListSize)
                .Select(p => new DashboardLowStockRow
                {
                    Id = p.Id,
                    Reference = p.Reference,
                    Name = p.Name,
                    Quantity = p.Quantity,
                    MinimumStock = p.MinimumStock
                })
                .ToListAsync();

            var recentMovements = await _context.StockMovements
                .AsNoTracking()
                .Where(m => !m.Product.IsArchived)
                .OrderByDescending(m => m.CreatedAt)
                .ThenByDescending(m => m.Id)
                .Take(ListSize)
                .Select(m => new DashboardMovementRow
                {
                    ProductId = m.ProductId,
                    ProductReference = m.Product.Reference,
                    ProductName = m.Product.Name,
                    MovementType = m.MovementType,
                    QuantityChange = m.QuantityChange,
                    QuantityAfter = m.QuantityAfter,
                    CreatedAt = m.CreatedAt
                })
                .ToListAsync();

            return new DashboardViewModel
            {
                ActiveProductCount = activeProductCount,
                CategoryCount = categoryCount,
                LowStockCount = lowStockCount,
                TotalStockValue = totalStockValue,
                LowStockProducts = lowStockProducts,
                RecentMovements = recentMovements
            };
        }
    }
}