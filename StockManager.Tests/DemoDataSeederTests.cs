using Microsoft.EntityFrameworkCore;
using StockManager.Data;
using StockManager.Models;
using StockManager.Services;

namespace StockManager.Tests
{
    public class DemoDataSeederTests
    {
        private sealed class Env : IDisposable
        {
            public TestDatabase Db { get; } = new();
            public ApplicationDbContext Context { get; }
            public DemoDataSeeder Seeder { get; }
            public DashboardService Dashboard { get; }
            public ReorderService Reorder { get; }

            public Env()
            {
                Context = Db.CreateContext();

                var orders = new PurchaseOrderService(Context);

                Seeder = new DemoDataSeeder(
                    Context,
                    new CategoryService(Context),
                    new SupplierService(Context),
                    new ProductService(Context),
                    new StockService(Context),
                    orders);

                Dashboard = new DashboardService(Context);
                Reorder = new ReorderService(Context, orders);
            }

            public void Dispose()
            {
                Context.Dispose();
                Db.Dispose();
            }
        }

        [Fact]
        public async Task Seed_OnAnEmptyDatabase_CreatesCategoriesSuppliersProductsOrdersAndMovements()
        {
            using var env = new Env();

            var result = await env.Seeder.SeedAsync();

            Assert.True(result.Seeded);
            Assert.Contains("16 Produkte", result.Message);
            Assert.Equal(5, await env.Context.Categories.CountAsync());
            Assert.Equal(4, await env.Context.Suppliers.CountAsync(s => !s.IsArchived));
            Assert.Equal(1, await env.Context.Suppliers.CountAsync(s => s.IsArchived));
            Assert.Equal(16, await env.Context.Products.CountAsync(p => !p.IsArchived));
            Assert.Equal(6, await env.Context.PurchaseOrders.CountAsync());
            Assert.Equal(52, await env.Context.StockMovements.CountAsync());
        }

        [Fact]
        public async Task Seed_Twice_DoesNothingTheSecondTime()
        {
            using var env = new Env();
            await env.Seeder.SeedAsync();

            var productsBefore = await env.Context.Products.CountAsync();
            var movementsBefore = await env.Context.StockMovements.CountAsync();

            var second = await env.Seeder.SeedAsync();

            Assert.False(second.Seeded);
            Assert.Equal(productsBefore, await env.Context.Products.CountAsync());
            Assert.Equal(movementsBefore, await env.Context.StockMovements.CountAsync());
        }

        [Theory]
        [InlineData(true)]
        [InlineData(false)]
        public async Task Seed_WhenABusinessTableAlreadyHasData_IsSkipped(bool withProduct)
        {
            using var env = new Env();

            if (withProduct)
            {
                env.Db.AddProduct(quantity: 0);
            }
            else
            {
                env.Db.AddCategory();
            }

            var result = await env.Seeder.SeedAsync();

            Assert.False(result.Seeded);
            Assert.Equal(0, await env.Context.Suppliers.CountAsync());
            Assert.Equal(0, await env.Context.PurchaseOrders.CountAsync());
        }

        [Fact]
        public async Task Seed_CreatesOrdersInEveryStatus()
        {
            using var env = new Env();
            await env.Seeder.SeedAsync();

            var statuses = await env.Context.PurchaseOrders.Select(o => o.Status).Distinct().ToListAsync();

            Assert.Equal(
                Enum.GetValues<PurchaseOrderStatus>().OrderBy(s => (int)s),
                statuses.OrderBy(s => (int)s));
        }

        [Fact]
        public async Task Seed_OrdersAreInTheExpectedStates()
        {
            using var env = new Env();
            await env.Seeder.SeedAsync();

            var orders = await env.Context.PurchaseOrders.AsNoTracking().Include(o => o.Lines).ToListAsync();

            PurchaseOrder Get(PurchaseOrderStatus status) => orders.Single(o => o.Status == status);

            var received = Get(PurchaseOrderStatus.Received);
            Assert.All(received.Lines, l => Assert.Equal(l.QuantityOrdered, l.QuantityReceived));

            var partial = Get(PurchaseOrderStatus.PartiallyReceived);
            Assert.Contains(partial.Lines, l => l.QuantityReceived > 0 && l.QuantityReceived < l.QuantityOrdered);

            var closed = Get(PurchaseOrderStatus.Closed);
            Assert.Contains(closed.Lines, l => l.QuantityReceived > 0 && l.QuantityReceived < l.QuantityOrdered);

            var ordered = Get(PurchaseOrderStatus.Ordered);
            Assert.All(ordered.Lines, l => Assert.Equal(0, l.QuantityReceived));
            Assert.NotNull(ordered.OrderedAt);

            var cancelled = Get(PurchaseOrderStatus.Cancelled);
            Assert.All(cancelled.Lines, l => Assert.Equal(0, l.QuantityReceived));

            var draft = Get(PurchaseOrderStatus.Draft);
            Assert.NotEmpty(draft.Lines);
            Assert.Null(draft.OrderedAt);
        }

        [Fact]
        public async Task Seed_MovementsAreConsistentForEveryProduct()
        {
            using var env = new Env();
            await env.Seeder.SeedAsync();

            var products = await env.Context.Products.AsNoTracking().ToListAsync();
            var movements = await env.Context.StockMovements.AsNoTracking().OrderBy(m => m.Id).ToListAsync();

            foreach (var product in products)
            {
                var own = movements.Where(m => m.ProductId == product.Id).ToList();
                Assert.NotEmpty(own);

                var running = 0;
                DateTime? previous = null;

                foreach (var movement in own)
                {
                    running += movement.QuantityChange;
                    Assert.Equal(running, movement.QuantityAfter);

                    if (previous != null)
                    {
                        Assert.True(movement.CreatedAt >= previous.Value);
                    }

                    previous = movement.CreatedAt;
                }

                Assert.Equal(running, product.Quantity);
            }
        }

        [Fact]
        public async Task Seed_SpreadsTheDatesOverFourWeeks_InChronologicalOrder()
        {
            using var env = new Env();
            await env.Seeder.SeedAsync();

            var now = DateTime.UtcNow;

            var movements = await env.Context.StockMovements.AsNoTracking().ToListAsync();
            Assert.True(movements.Min(m => m.CreatedAt) < now.AddDays(-20));
            Assert.True(movements.Max(m => m.CreatedAt) <= now.AddMinutes(1));

            var orders = await env.Context.PurchaseOrders.AsNoTracking().OrderBy(o => o.Id).ToListAsync();

            for (var i = 1; i < orders.Count; i++)
            {
                Assert.True(orders[i].CreatedAt >= orders[i - 1].CreatedAt);
            }

            foreach (var order in orders)
            {
                if (order.OrderedAt != null)
                {
                    Assert.True(order.OrderedAt >= order.CreatedAt);
                }

                if (order.LastReceiptAt != null)
                {
                    Assert.NotNull(order.OrderedAt);
                    Assert.True(order.LastReceiptAt >= order.OrderedAt);
                }
            }

            var receipts = await env.Context.StockMovements
                .AsNoTracking()
                .Where(m => m.PurchaseOrderLineId != null)
                .Include(m => m.PurchaseOrderLine!)
                .ThenInclude(l => l.PurchaseOrder)
                .ToListAsync();

            Assert.NotEmpty(receipts);

            foreach (var receipt in receipts)
            {
                var orderedAt = receipt.PurchaseOrderLine!.PurchaseOrder.OrderedAt;
                Assert.NotNull(orderedAt);
                Assert.True(receipt.CreatedAt >= orderedAt);
            }
        }

        [Fact]
        public async Task Dashboard_AfterSeeding_ShowsTheExpectedFigures()
        {
            using var env = new Env();
            await env.Seeder.SeedAsync();

            var dashboard = await env.Dashboard.GetDashboardAsync();

            Assert.Equal(16, dashboard.ActiveProductCount);
            Assert.Equal(5, dashboard.CategoryCount);
            Assert.Equal(5, dashboard.LowStockCount);
            Assert.Equal(5, dashboard.LowStockProducts.Count);
            Assert.Equal(5, dashboard.RecentMovements.Count);
            Assert.Equal(3051.25m, dashboard.TotalStockValue);
        }

        [Fact]
        public async Task Reorder_AfterSeeding_GroupsTwoSuppliersAndListsOneProductWithoutSupplier()
        {
            using var env = new Env();
            await env.Seeder.SeedAsync();

            var suggestions = await env.Reorder.GetSuggestionsAsync();

            Assert.False(suggestions.IsEmpty);
            Assert.Equal(
                new[] { "Schraubenwerk Ruhr GmbH", "Verpackung Nord AG" },
                suggestions.Groups.Select(g => g.SupplierName).ToArray());

            var fasteners = suggestions.Groups[0];
            Assert.Equal(
                new[] { "DUB-8", "MUT-M6" },
                fasteners.Items.Select(i => i.Reference).OrderBy(r => r).ToArray());
            Assert.All(fasteners.Items, i => Assert.Equal(35, i.SuggestedQuantity));

            var packaging = Assert.Single(suggestions.Groups[1].Items);
            Assert.Equal("ETI-100", packaging.Reference);
            Assert.Equal(33, packaging.SuggestedQuantity);

            var withoutSupplier = Assert.Single(suggestions.WithoutSupplier);
            Assert.Equal("HAN-L", withoutSupplier.Reference);
            Assert.Equal(40, withoutSupplier.SuggestedQuantity);
        }

        [Fact]
        public async Task Seed_RespectsTheBusinessRules()
        {
            using var env = new Env();
            await env.Seeder.SeedAsync();

            Assert.Empty(await env.Context.Products.Where(p => p.Quantity < 0).ToListAsync());

            var references = await env.Context.Products.Select(p => p.Reference).ToListAsync();
            Assert.Equal(references.Count, references.Distinct().Count());

            var retired = await env.Context.Suppliers.SingleAsync(s => s.IsArchived);
            Assert.False(await env.Context.Products.AnyAsync(p => p.PreferredSupplierId == retired.Id));
            Assert.False(await env.Context.PurchaseOrders.AnyAsync(o => o.SupplierId == retired.Id));

            Assert.Equal(2, await env.Context.Products.CountAsync(p => p.PreferredSupplierId == null));
        }
    }
}