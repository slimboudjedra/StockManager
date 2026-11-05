using Microsoft.EntityFrameworkCore;
using StockManager.Models;
using StockManager.Services;

namespace StockManager.Tests
{
    public class DashboardServiceTests
    {
        [Fact]
        public async Task EmptyDatabase_ReturnsZerosAndEmptyLists()
        {
            using var db = new TestDatabase();
            using var context = db.CreateContext();
            var service = new DashboardService(context);

            var dashboard = await service.GetDashboardAsync();

            Assert.Equal(0, dashboard.ActiveProductCount);
            Assert.Equal(0, dashboard.CategoryCount);
            Assert.Equal(0, dashboard.LowStockCount);
            Assert.Equal(0m, dashboard.TotalStockValue);
            Assert.Empty(dashboard.LowStockProducts);
            Assert.Empty(dashboard.RecentMovements);
        }

        [Fact]
        public async Task ProductCount_IgnoresArchivedProducts()
        {
            using var db = new TestDatabase();
            db.AddProduct(quantity: 5);
            db.AddProduct(quantity: 5);
            db.AddProduct(quantity: 5, isArchived: true);
            using var context = db.CreateContext();
            var service = new DashboardService(context);

            var dashboard = await service.GetDashboardAsync();

            Assert.Equal(2, dashboard.ActiveProductCount);
        }

        [Fact]
        public async Task TotalStockValue_SumsQuantityTimesPrice_OfActiveProductsOnly()
        {
            using var db = new TestDatabase();
            db.AddProduct(quantity: 2, unitPrice: 10.50m);                    // 21,00
            db.AddProduct(quantity: 3, unitPrice: 2m);                        // 6,00
            db.AddProduct(quantity: 100, unitPrice: 5m, isArchived: true);    // zählt nicht
            using var context = db.CreateContext();
            var service = new DashboardService(context);

            var dashboard = await service.GetDashboardAsync();

            Assert.Equal(27.00m, dashboard.TotalStockValue);
        }

        [Fact]
        public async Task LowStock_CountsAtOrBelowMinimum_AndListsMostCriticalFirst()
        {
            using var db = new TestDatabase();
            var exactlyAtMinimum = db.AddProduct(quantity: 5, minimumStock: 5);
            var farBelowMinimum = db.AddProduct(quantity: 0, minimumStock: 10);
            db.AddProduct(quantity: 20, minimumStock: 5);                          // genug Bestand
            db.AddProduct(quantity: 0, minimumStock: 10, isArchived: true);        // archiviert
            using var context = db.CreateContext();
            var service = new DashboardService(context);

            var dashboard = await service.GetDashboardAsync();

            Assert.Equal(2, dashboard.LowStockCount);
            Assert.Equal(
                new[] { farBelowMinimum, exactlyAtMinimum },
                dashboard.LowStockProducts.Select(p => p.Id).ToArray());
        }

        [Fact]
        public async Task LowStockList_IsLimitedToFive_ButCountIsComplete()
        {
            using var db = new TestDatabase();
            for (var i = 0; i < 7; i++)
            {
                db.AddProduct(quantity: 0, minimumStock: 1);
            }
            using var context = db.CreateContext();
            var service = new DashboardService(context);

            var dashboard = await service.GetDashboardAsync();

            Assert.Equal(7, dashboard.LowStockCount);
            Assert.Equal(5, dashboard.LowStockProducts.Count);
        }

        [Fact]
        public async Task RecentMovements_AreNewestFirst_LimitedToFive_AndSkipArchivedProducts()
        {
            using var db = new TestDatabase();
            var productId = db.AddProduct(quantity: 0);
            var archivedId = db.AddProduct(quantity: 0);
            using var context = db.CreateContext();
            var stock = new StockService(context);
            var service = new DashboardService(context);

            for (var i = 1; i <= 6; i++)
            {
                await stock.RecordMovementAsync(productId, StockMovementType.Entry, i, null);
            }

            // Die jüngste Bewegung gehört zu einem Produkt, das danach archiviert wird.
            await stock.RecordMovementAsync(archivedId, StockMovementType.Entry, 100, null);
            var archived = await context.Products.SingleAsync(p => p.Id == archivedId);
            archived.IsArchived = true;
            await context.SaveChangesAsync();

            var dashboard = await service.GetDashboardAsync();

            Assert.Equal(
                new[] { 6, 5, 4, 3, 2 },
                dashboard.RecentMovements.Select(m => m.QuantityChange).ToArray());
        }
    }
}