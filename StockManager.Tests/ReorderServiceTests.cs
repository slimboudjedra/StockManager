using Microsoft.EntityFrameworkCore;
using StockManager.Data;
using StockManager.Models;
using StockManager.Services;
using StockManager.ViewModels;

namespace StockManager.Tests
{
    public class ReorderServiceTests
    {
        private sealed class Env : IDisposable
        {
            public TestDatabase Db { get; } = new();
            public ApplicationDbContext Context { get; }
            public SupplierService Suppliers { get; }
            public PurchaseOrderService Orders { get; }
            public ReorderService Reorder { get; }

            public Env()
            {
                Context = Db.CreateContext();
                Suppliers = new SupplierService(Context);
                Orders = new PurchaseOrderService(Context);
                Reorder = new ReorderService(Context, Orders);
            }

            public async Task<Supplier> AddSupplierAsync(string name = "Alpha GmbH") =>
                await Suppliers.CreateAsync(new SupplierEditViewModel { Name = name });

            public void Dispose()
            {
                Context.Dispose();
                Db.Dispose();
            }
        }

        // ---------- Welche Produkte vorgeschlagen werden ----------

        [Fact]
        public async Task ProductBelowMinimum_IsSuggested_UpToTwiceTheMinimum()
        {
            using var env = new Env();
            var supplier = await env.AddSupplierAsync();
            var productId = env.Db.AddProduct(quantity: 3, minimumStock: 10, unitPrice: 2.50m,
                preferredSupplierId: supplier.Id);

            var result = await env.Reorder.GetSuggestionsAsync();

            var group = Assert.Single(result.Groups);
            Assert.Equal(supplier.Id, group.SupplierId);

            var item = Assert.Single(group.Items);
            Assert.Equal(productId, item.ProductId);
            Assert.Equal(17, item.SuggestedQuantity);
            Assert.Equal(0, item.OpenQuantity);
            Assert.Equal(2.50m, item.UnitPrice);

            Assert.Empty(result.WithoutSupplier);
            Assert.False(result.IsEmpty);
        }

        [Fact]
        public async Task ProductExactlyAtMinimum_IsSuggested()
        {
            using var env = new Env();
            var supplier = await env.AddSupplierAsync();
            env.Db.AddProduct(quantity: 10, minimumStock: 10, preferredSupplierId: supplier.Id);

            var result = await env.Reorder.GetSuggestionsAsync();

            var item = Assert.Single(Assert.Single(result.Groups).Items);
            Assert.Equal(10, item.SuggestedQuantity);
        }

        [Fact]
        public async Task ProductAboveMinimum_IsNotSuggested()
        {
            using var env = new Env();
            var supplier = await env.AddSupplierAsync();
            env.Db.AddProduct(quantity: 11, minimumStock: 10, preferredSupplierId: supplier.Id);

            var result = await env.Reorder.GetSuggestionsAsync();

            Assert.True(result.IsEmpty);
        }

        [Fact]
        public async Task ProductWithoutAMinimum_IsNotSuggested()
        {
            using var env = new Env();
            var supplier = await env.AddSupplierAsync();
            env.Db.AddProduct(quantity: 0, minimumStock: 0, preferredSupplierId: supplier.Id);

            var result = await env.Reorder.GetSuggestionsAsync();

            Assert.True(result.IsEmpty);
        }

        [Fact]
        public async Task ArchivedProduct_IsNotSuggested()
        {
            using var env = new Env();
            var supplier = await env.AddSupplierAsync();
            env.Db.AddProduct(quantity: 0, isArchived: true, minimumStock: 10,
                preferredSupplierId: supplier.Id);

            var result = await env.Reorder.GetSuggestionsAsync();

            Assert.True(result.IsEmpty);
        }

        [Fact]
        public async Task ProductWithoutASupplier_IsListedSeparately()
        {
            using var env = new Env();
            var productId = env.Db.AddProduct(quantity: 0, minimumStock: 10);

            var result = await env.Reorder.GetSuggestionsAsync();

            Assert.Empty(result.Groups);
            var item = Assert.Single(result.WithoutSupplier);
            Assert.Equal(productId, item.ProductId);
            Assert.Equal(20, item.SuggestedQuantity);
            Assert.False(result.IsEmpty);
        }

        [Fact]
        public async Task Suggestions_AreGroupedBySupplier_SortedByName_WithTotals()
        {
            using var env = new Env();
            var zeta = await env.AddSupplierAsync("Zeta KG");
            var alpha = await env.AddSupplierAsync("Alpha GmbH");
            env.Db.AddProduct(quantity: 0, minimumStock: 10, unitPrice: 2m, preferredSupplierId: alpha.Id);   // 20 x 2  = 40
            env.Db.AddProduct(quantity: 5, minimumStock: 5, unitPrice: 3m, preferredSupplierId: alpha.Id);    //  5 x 3  = 15
            env.Db.AddProduct(quantity: 0, minimumStock: 4, unitPrice: 10m, preferredSupplierId: zeta.Id);    //  8 x 10 = 80

            var result = await env.Reorder.GetSuggestionsAsync();

            Assert.Equal(2, result.Groups.Count);
            Assert.Equal("Alpha GmbH", result.Groups[0].SupplierName);
            Assert.Equal(2, result.Groups[0].Items.Count);
            Assert.Equal(55m, result.Groups[0].TotalAmount);
            Assert.Equal("Zeta KG", result.Groups[1].SupplierName);
            Assert.Single(result.Groups[1].Items);
            Assert.Equal(80m, result.Groups[1].TotalAmount);
        }

        // ---------- Bereits bestellte und geplante Mengen ----------

        [Fact]
        public async Task ProductInADraft_IsCoveredAndNotSuggested()
        {
            using var env = new Env();
            var supplier = await env.AddSupplierAsync();
            var productId = env.Db.AddProduct(quantity: 3, minimumStock: 10, preferredSupplierId: supplier.Id);
            var draft = await env.Orders.CreateDraftAsync(new PurchaseOrderEditViewModel { SupplierId = supplier.Id });
            await env.Orders.AddLineAsync(draft.Id, productId, 20, null);

            var result = await env.Reorder.GetSuggestionsAsync();

            Assert.True(result.IsEmpty);
        }

        [Fact]
        public async Task PlacedOrder_ThatFullyCoversTheMinimum_RemovesTheSuggestion()
        {
            using var env = new Env();
            var supplier = await env.AddSupplierAsync();
            var productId = env.Db.AddProduct(quantity: 3, minimumStock: 10, preferredSupplierId: supplier.Id);
            var order = await env.Orders.CreateDraftAsync(new PurchaseOrderEditViewModel { SupplierId = supplier.Id });
            await env.Orders.AddLineAsync(order.Id, productId, 8, null);   // 3 + 8 = 11 > 10
            await env.Orders.PlaceOrderAsync(order.Id);

            var result = await env.Reorder.GetSuggestionsAsync();

            Assert.True(result.IsEmpty);
        }

        [Fact]
        public async Task PlacedOrder_ThatOnlyPartlyCovers_ReducesTheSuggestedQuantity()
        {
            using var env = new Env();
            var supplier = await env.AddSupplierAsync();
            var productId = env.Db.AddProduct(quantity: 3, minimumStock: 10, preferredSupplierId: supplier.Id);
            var order = await env.Orders.CreateDraftAsync(new PurchaseOrderEditViewModel { SupplierId = supplier.Id });
            await env.Orders.AddLineAsync(order.Id, productId, 5, null);   // 3 + 5 = 8 <= 10
            await env.Orders.PlaceOrderAsync(order.Id);

            var result = await env.Reorder.GetSuggestionsAsync();

            var item = Assert.Single(Assert.Single(result.Groups).Items);
            Assert.Equal(5, item.OpenQuantity);
            Assert.Equal(12, item.SuggestedQuantity);   // 20 - (3 + 5)
        }

        [Fact]
        public async Task CancelledOrder_DoesNotCount()
        {
            using var env = new Env();
            var supplier = await env.AddSupplierAsync();
            var productId = env.Db.AddProduct(quantity: 3, minimumStock: 10, preferredSupplierId: supplier.Id);
            var draft = await env.Orders.CreateDraftAsync(new PurchaseOrderEditViewModel { SupplierId = supplier.Id });
            await env.Orders.AddLineAsync(draft.Id, productId, 20, null);
            await env.Orders.CancelAsync(draft.Id);

            var result = await env.Reorder.GetSuggestionsAsync();

            var item = Assert.Single(Assert.Single(result.Groups).Items);
            Assert.Equal(0, item.OpenQuantity);
            Assert.Equal(17, item.SuggestedQuantity);
        }

        // ---------- Entwürfe anlegen ----------

        [Fact]
        public async Task CreateDrafts_ForOneSupplier_CreatesOneDraftWithTheSuggestedLines()
        {
            using var env = new Env();
            var supplier = await env.AddSupplierAsync();
            var first = env.Db.AddProduct(quantity: 0, minimumStock: 10, unitPrice: 2.50m,
                preferredSupplierId: supplier.Id);
            var second = env.Db.AddProduct(quantity: 4, minimumStock: 5, unitPrice: 10m,
                preferredSupplierId: supplier.Id);

            var orders = await env.Reorder.CreateDraftsAsync(supplier.Id);

            var order = Assert.Single(orders);
            var details = await env.Orders.GetDetailsAsync(order.Id);

            Assert.NotNull(details);
            Assert.Equal(PurchaseOrderStatus.Draft, details.Status);
            Assert.Equal(supplier.Id, details.SupplierId);
            Assert.Contains("Nachbestellvorschlag", details.Notes);
            Assert.Equal(2, details.Lines.Count);

            var firstLine = details.Lines.Single(l => l.ProductId == first);
            Assert.Equal(20, firstLine.QuantityOrdered);
            Assert.Equal(2.50m, firstLine.PurchasePrice);

            var secondLine = details.Lines.Single(l => l.ProductId == second);
            Assert.Equal(6, secondLine.QuantityOrdered);
            Assert.Equal(10m, secondLine.PurchasePrice);

            // Die Produkte sind jetzt durch den Entwurf gedeckt.
            Assert.True((await env.Reorder.GetSuggestionsAsync()).IsEmpty);

            // Ein zweiter Klick findet nichts mehr und legt keinen Doppelgänger an.
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                env.Reorder.CreateDraftsAsync(supplier.Id));
            Assert.Equal(1, await env.Context.PurchaseOrders.CountAsync());
        }

        [Fact]
        public async Task CreateDrafts_ForAllSuppliers_CreatesOneDraftPerSupplier()
        {
            using var env = new Env();
            var alpha = await env.AddSupplierAsync("Alpha GmbH");
            var beta = await env.AddSupplierAsync("Beta AG");
            env.Db.AddProduct(quantity: 0, minimumStock: 10, preferredSupplierId: alpha.Id);
            env.Db.AddProduct(quantity: 0, minimumStock: 10, preferredSupplierId: beta.Id);
            env.Db.AddProduct(quantity: 0, minimumStock: 10);   // ohne Lieferant

            var orders = await env.Reorder.CreateDraftsAsync(null);

            Assert.Equal(2, orders.Count);
            Assert.Equal(
                new[] { alpha.Id, beta.Id }.OrderBy(i => i).ToArray(),
                orders.Select(o => o.SupplierId).OrderBy(i => i).ToArray());
            Assert.Equal(2, orders.Select(o => o.OrderNumber).Distinct().Count());

            var after = await env.Reorder.GetSuggestionsAsync();
            Assert.Empty(after.Groups);
            Assert.Single(after.WithoutSupplier);
        }

        [Fact]
        public async Task CreateDrafts_WithoutAnySuggestion_IsRejected()
        {
            using var env = new Env();
            var supplier = await env.AddSupplierAsync();

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                env.Reorder.CreateDraftsAsync(supplier.Id));
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                env.Reorder.CreateDraftsAsync(null));
            Assert.Equal(0, await env.Context.PurchaseOrders.CountAsync());
        }

        // ---------- Entwurf mit Positionen in einem Schritt ----------

        [Fact]
        public async Task CreateDraftWithLines_WithoutLines_IsRejected()
        {
            using var env = new Env();
            var supplier = await env.AddSupplierAsync();

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                env.Orders.CreateDraftWithLinesAsync(supplier.Id, new List<DraftLineInput>(), null));

            Assert.Equal(0, await env.Context.PurchaseOrders.CountAsync());
        }

        [Fact]
        public async Task CreateDraftWithLines_SameProductTwice_IsRejected_AndNothingIsCreated()
        {
            using var env = new Env();
            var supplier = await env.AddSupplierAsync();
            var productId = env.Db.AddProduct(quantity: 0);

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                env.Orders.CreateDraftWithLinesAsync(supplier.Id, new List<DraftLineInput>
                {
                    new(productId, 1, 1m),
                    new(productId, 2, 1m)
                }, null));

            Assert.Equal(0, await env.Context.PurchaseOrders.CountAsync());
            Assert.Equal(0, await env.Context.PurchaseOrderLines.CountAsync());
        }

        [Fact]
        public async Task CreateDraftWithLines_ArchivedOrUnknownProduct_IsRejected_AndNothingIsCreated()
        {
            using var env = new Env();
            var supplier = await env.AddSupplierAsync();
            var good = env.Db.AddProduct(quantity: 0);
            var archived = env.Db.AddProduct(quantity: 0, isArchived: true);

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                env.Orders.CreateDraftWithLinesAsync(supplier.Id, new List<DraftLineInput>
                {
                    new(good, 1, 1m),
                    new(archived, 1, 1m)
                }, null));
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                env.Orders.CreateDraftWithLinesAsync(supplier.Id, new List<DraftLineInput>
                {
                    new(9999, 1, 1m)
                }, null));

            Assert.Equal(0, await env.Context.PurchaseOrders.CountAsync());
        }

        [Fact]
        public async Task CreateDraftWithLines_ArchivedSupplier_IsRejected()
        {
            using var env = new Env();
            var supplier = await env.AddSupplierAsync();
            var productId = env.Db.AddProduct(quantity: 0);
            await env.Suppliers.ArchiveAsync(supplier.Id);

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                env.Orders.CreateDraftWithLinesAsync(supplier.Id, new List<DraftLineInput>
                {
                    new(productId, 1, 1m)
                }, null));
        }

        [Fact]
        public async Task CreateDraftWithLines_QuantityBelowOne_IsRejected()
        {
            using var env = new Env();
            var supplier = await env.AddSupplierAsync();
            var productId = env.Db.AddProduct(quantity: 0);

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                env.Orders.CreateDraftWithLinesAsync(supplier.Id, new List<DraftLineInput>
                {
                    new(productId, 0, 1m)
                }, null));

            Assert.Equal(0, await env.Context.PurchaseOrders.CountAsync());
        }
    }
}