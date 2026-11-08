using Microsoft.EntityFrameworkCore;
using StockManager.Data;
using StockManager.Models;
using StockManager.Services;
using StockManager.ViewModels;

namespace StockManager.Tests
{
    public class PurchaseOrderServiceTests
    {
        private sealed class Env : IDisposable
        {
            public TestDatabase Db { get; } = new();
            public ApplicationDbContext Context { get; }
            public SupplierService Suppliers { get; }
            public PurchaseOrderService Orders { get; }

            public Env()
            {
                Context = Db.CreateContext();
                Suppliers = new SupplierService(Context);
                Orders = new PurchaseOrderService(Context);
            }

            public async Task<Supplier> AddSupplierAsync(string name = "Alpha GmbH") =>
                await Suppliers.CreateAsync(new SupplierEditViewModel { Name = name });

            public async Task<PurchaseOrder> AddDraftAsync(int supplierId) =>
                await Orders.CreateDraftAsync(new PurchaseOrderEditViewModel { SupplierId = supplierId });

            public void Dispose()
            {
                Context.Dispose();
                Db.Dispose();
            }
        }

        // ---------- Entwurf anlegen ----------

        [Fact]
        public async Task CreateDraft_SetsStatusSupplierAndNumber()
        {
            using var env = new Env();
            var supplier = await env.AddSupplierAsync();

            var order = await env.AddDraftAsync(supplier.Id);

            Assert.Equal(PurchaseOrderStatus.Draft, order.Status);
            Assert.Equal(supplier.Id, order.SupplierId);
            Assert.Equal($"B-{DateTime.UtcNow.Year}-00001", order.OrderNumber);
            Assert.Null(order.OrderedAt);
        }

        [Fact]
        public async Task CreateDraft_NumbersCountUp()
        {
            using var env = new Env();
            var supplier = await env.AddSupplierAsync();

            await env.AddDraftAsync(supplier.Id);
            var second = await env.AddDraftAsync(supplier.Id);

            Assert.Equal($"B-{DateTime.UtcNow.Year}-00002", second.OrderNumber);
        }

        [Fact]
        public async Task CreateDraft_WithAnArchivedSupplier_IsRejected()
        {
            using var env = new Env();
            var supplier = await env.AddSupplierAsync();
            await env.Suppliers.ArchiveAsync(supplier.Id);

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                env.AddDraftAsync(supplier.Id));
        }

        [Fact]
        public async Task CreateDraft_WithAnUnknownSupplier_IsRejected()
        {
            using var env = new Env();

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                env.AddDraftAsync(9999));
        }

        [Fact]
        public async Task UpdateHeader_ChangesSupplierDateAndNotes()
        {
            using var env = new Env();
            var alpha = await env.AddSupplierAsync("Alpha GmbH");
            var beta = await env.AddSupplierAsync("Beta AG");
            var order = await env.AddDraftAsync(alpha.Id);

            var updated = await env.Orders.UpdateHeaderAsync(order.Id, new PurchaseOrderEditViewModel
            {
                Id = order.Id,
                SupplierId = beta.Id,
                ExpectedDeliveryDate = new DateOnly(2026, 12, 1),
                Notes = "  Bitte vormittags liefern  "
            });

            var edit = await env.Orders.GetForEditAsync(order.Id);

            Assert.True(updated);
            Assert.NotNull(edit);
            Assert.Equal(beta.Id, edit.SupplierId);
            Assert.Equal(new DateOnly(2026, 12, 1), edit.ExpectedDeliveryDate!.Value);
            Assert.Equal("Bitte vormittags liefern", edit.Notes);
        }

        // ---------- Positionen ----------

        [Fact]
        public async Task AddLine_WithoutPrice_UsesTheProductPrice()
        {
            using var env = new Env();
            var supplier = await env.AddSupplierAsync();
            var order = await env.AddDraftAsync(supplier.Id);
            var productId = env.Db.AddProduct(quantity: 0, unitPrice: 12.50m);

            var line = await env.Orders.AddLineAsync(order.Id, productId, 4, null);

            Assert.NotNull(line);
            Assert.Equal(12.50m, line.PurchasePrice);
            Assert.Equal(4, line.QuantityOrdered);
            Assert.Equal(0, line.QuantityReceived);
        }

        [Fact]
        public async Task AddLine_WithExplicitPrice_KeepsIt()
        {
            using var env = new Env();
            var supplier = await env.AddSupplierAsync();
            var order = await env.AddDraftAsync(supplier.Id);
            var productId = env.Db.AddProduct(quantity: 0, unitPrice: 12.50m);

            var line = await env.Orders.AddLineAsync(order.Id, productId, 4, 9.90m);

            Assert.NotNull(line);
            Assert.Equal(9.90m, line.PurchasePrice);
        }

        [Fact]
        public async Task AddLine_SameProductTwice_IsRejected()
        {
            using var env = new Env();
            var supplier = await env.AddSupplierAsync();
            var order = await env.AddDraftAsync(supplier.Id);
            var productId = env.Db.AddProduct(quantity: 0);

            await env.Orders.AddLineAsync(order.Id, productId, 1, null);

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                env.Orders.AddLineAsync(order.Id, productId, 2, null));
        }

        [Fact]
        public async Task AddLine_QuantityBelowOne_IsRejected()
        {
            using var env = new Env();
            var supplier = await env.AddSupplierAsync();
            var order = await env.AddDraftAsync(supplier.Id);
            var productId = env.Db.AddProduct(quantity: 0);

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                env.Orders.AddLineAsync(order.Id, productId, 0, null));
        }

        [Fact]
        public async Task AddLine_ArchivedProduct_IsRejected()
        {
            using var env = new Env();
            var supplier = await env.AddSupplierAsync();
            var order = await env.AddDraftAsync(supplier.Id);
            var productId = env.Db.AddProduct(quantity: 0, isArchived: true);

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                env.Orders.AddLineAsync(order.Id, productId, 1, null));
        }

        [Fact]
        public async Task AddLine_UnknownOrder_ReturnsNull()
        {
            using var env = new Env();
            var productId = env.Db.AddProduct(quantity: 0);

            var line = await env.Orders.AddLineAsync(9999, productId, 1, null);

            Assert.Null(line);
        }

        [Fact]
        public async Task UpdateLine_ChangesQuantityAndPrice()
        {
            using var env = new Env();
            var supplier = await env.AddSupplierAsync();
            var order = await env.AddDraftAsync(supplier.Id);
            var productId = env.Db.AddProduct(quantity: 0, unitPrice: 10m);
            var line = await env.Orders.AddLineAsync(order.Id, productId, 2, null);
            Assert.NotNull(line);

            var updated = await env.Orders.UpdateLineAsync(line.Id, 7, 3.25m);
            var details = await env.Orders.GetDetailsAsync(order.Id);

            Assert.True(updated);
            Assert.NotNull(details);
            Assert.Equal(7, details.Lines[0].QuantityOrdered);
            Assert.Equal(3.25m, details.Lines[0].PurchasePrice);
        }

        [Fact]
        public async Task RemoveLine_RemovesTheLine()
        {
            using var env = new Env();
            var supplier = await env.AddSupplierAsync();
            var order = await env.AddDraftAsync(supplier.Id);
            var productId = env.Db.AddProduct(quantity: 0);
            var line = await env.Orders.AddLineAsync(order.Id, productId, 1, null);
            Assert.NotNull(line);

            var removed = await env.Orders.RemoveLineAsync(line.Id);
            var again = await env.Orders.RemoveLineAsync(line.Id);
            var details = await env.Orders.GetDetailsAsync(order.Id);

            Assert.True(removed);
            Assert.False(again);
            Assert.NotNull(details);
            Assert.Empty(details.Lines);
        }

        // ---------- Bestellung aufgeben ----------

        [Fact]
        public async Task PlaceOrder_WithoutLines_IsRejected()
        {
            using var env = new Env();
            var supplier = await env.AddSupplierAsync();
            var order = await env.AddDraftAsync(supplier.Id);

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                env.Orders.PlaceOrderAsync(order.Id));
        }

        [Fact]
        public async Task PlaceOrder_SetsStatusAndDate()
        {
            using var env = new Env();
            var supplier = await env.AddSupplierAsync();
            var order = await env.AddDraftAsync(supplier.Id);
            var productId = env.Db.AddProduct(quantity: 0);
            await env.Orders.AddLineAsync(order.Id, productId, 1, null);

            var placed = await env.Orders.PlaceOrderAsync(order.Id);
            var details = await env.Orders.GetDetailsAsync(order.Id);

            Assert.True(placed);
            Assert.NotNull(details);
            Assert.Equal(PurchaseOrderStatus.Ordered, details.Status);
            Assert.NotNull(details.OrderedAt);
        }

        [Fact]
        public async Task PlacedOrder_CannotBeEdited()
        {
            using var env = new Env();
            var supplier = await env.AddSupplierAsync();
            var order = await env.AddDraftAsync(supplier.Id);
            var firstProduct = env.Db.AddProduct(quantity: 0);
            var secondProduct = env.Db.AddProduct(quantity: 0);
            var line = await env.Orders.AddLineAsync(order.Id, firstProduct, 1, null);
            Assert.NotNull(line);
            await env.Orders.PlaceOrderAsync(order.Id);

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                env.Orders.AddLineAsync(order.Id, secondProduct, 1, null));
            Assert.Contains("Bestellt", ex.Message);

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                env.Orders.UpdateLineAsync(line.Id, 5, 1m));
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                env.Orders.RemoveLineAsync(line.Id));
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                env.Orders.UpdateHeaderAsync(order.Id, new PurchaseOrderEditViewModel { SupplierId = supplier.Id }));
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                env.Orders.PlaceOrderAsync(order.Id));
        }

        // ---------- Stornieren und löschen ----------

        [Fact]
        public async Task Cancel_Draft_Works()
        {
            using var env = new Env();
            var supplier = await env.AddSupplierAsync();
            var order = await env.AddDraftAsync(supplier.Id);

            var cancelled = await env.Orders.CancelAsync(order.Id);
            var details = await env.Orders.GetDetailsAsync(order.Id);

            Assert.True(cancelled);
            Assert.NotNull(details);
            Assert.Equal(PurchaseOrderStatus.Cancelled, details.Status);
        }

        [Fact]
        public async Task Cancel_PlacedOrder_Works()
        {
            using var env = new Env();
            var supplier = await env.AddSupplierAsync();
            var order = await env.AddDraftAsync(supplier.Id);
            var productId = env.Db.AddProduct(quantity: 0);
            await env.Orders.AddLineAsync(order.Id, productId, 1, null);
            await env.Orders.PlaceOrderAsync(order.Id);

            var cancelled = await env.Orders.CancelAsync(order.Id);
            var details = await env.Orders.GetDetailsAsync(order.Id);

            Assert.True(cancelled);
            Assert.NotNull(details);
            Assert.Equal(PurchaseOrderStatus.Cancelled, details.Status);
        }

        [Fact]
        public async Task Cancel_AlreadyCancelled_IsRejected()
        {
            using var env = new Env();
            var supplier = await env.AddSupplierAsync();
            var order = await env.AddDraftAsync(supplier.Id);
            await env.Orders.CancelAsync(order.Id);

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                env.Orders.CancelAsync(order.Id));

            Assert.Contains("bereits storniert", ex.Message);
        }

        [Fact]
        public async Task Cancel_WhenGoodsWereReceived_IsRejected()
        {
            using var env = new Env();
            var supplier = await env.AddSupplierAsync();
            var order = await env.AddDraftAsync(supplier.Id);
            var productId = env.Db.AddProduct(quantity: 0);
            await env.Orders.AddLineAsync(order.Id, productId, 5, null);
            await env.Orders.PlaceOrderAsync(order.Id);

            // Wareneingang simulieren (die echte Funktion folgt in einem späteren Schritt)
            var line = await env.Context.PurchaseOrderLines.SingleAsync(l => l.PurchaseOrderId == order.Id);
            line.QuantityReceived = 2;
            var tracked = await env.Context.PurchaseOrders.SingleAsync(o => o.Id == order.Id);
            tracked.Status = PurchaseOrderStatus.PartiallyReceived;
            await env.Context.SaveChangesAsync();

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                env.Orders.CancelAsync(order.Id));

            Assert.Contains("Wareneingang", ex.Message);
        }

        [Fact]
        public async Task DeleteDraft_RemovesOrderAndLines()
        {
            using var env = new Env();
            var supplier = await env.AddSupplierAsync();
            var order = await env.AddDraftAsync(supplier.Id);
            var productId = env.Db.AddProduct(quantity: 0);
            await env.Orders.AddLineAsync(order.Id, productId, 1, null);

            var deleted = await env.Orders.DeleteDraftAsync(order.Id);

            Assert.True(deleted);
            Assert.Null(await env.Orders.GetDetailsAsync(order.Id));
            Assert.Equal(0, await env.Context.PurchaseOrderLines.CountAsync());
        }

        [Fact]
        public async Task DeleteDraft_OfAPlacedOrder_IsRejected()
        {
            using var env = new Env();
            var supplier = await env.AddSupplierAsync();
            var order = await env.AddDraftAsync(supplier.Id);
            var productId = env.Db.AddProduct(quantity: 0);
            await env.Orders.AddLineAsync(order.Id, productId, 1, null);
            await env.Orders.PlaceOrderAsync(order.Id);

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                env.Orders.DeleteDraftAsync(order.Id));

            Assert.NotNull(await env.Orders.GetDetailsAsync(order.Id));
        }

        // ---------- Lesen ----------

        [Fact]
        public async Task GetDetails_ComputesTotalsAndFlags()
        {
            using var env = new Env();
            var supplier = await env.AddSupplierAsync();
            var order = await env.AddDraftAsync(supplier.Id);
            var first = env.Db.AddProduct(quantity: 0, unitPrice: 10m);
            var second = env.Db.AddProduct(quantity: 0, unitPrice: 5m);
            await env.Orders.AddLineAsync(order.Id, first, 2, null);
            await env.Orders.AddLineAsync(order.Id, second, 3, null);

            var draft = await env.Orders.GetDetailsAsync(order.Id);

            Assert.NotNull(draft);
            Assert.Equal(2, draft.Lines.Count);
            Assert.Equal(35m, draft.TotalAmount);
            Assert.True(draft.CanEdit);
            Assert.True(draft.CanPlace);
            Assert.True(draft.CanDelete);

            await env.Orders.PlaceOrderAsync(order.Id);
            var placed = await env.Orders.GetDetailsAsync(order.Id);

            Assert.NotNull(placed);
            Assert.False(placed.CanEdit);
            Assert.False(placed.CanPlace);
            Assert.True(placed.CanCancel);
            Assert.False(placed.CanDelete);
        }

        [Fact]
        public async Task GetOrders_FiltersByStatus_AndListsNewestFirst()
        {
            using var env = new Env();
            var supplier = await env.AddSupplierAsync();
            var first = await env.AddDraftAsync(supplier.Id);
            var second = await env.AddDraftAsync(supplier.Id);
            var productId = env.Db.AddProduct(quantity: 0);
            await env.Orders.AddLineAsync(second.Id, productId, 1, null);
            await env.Orders.PlaceOrderAsync(second.Id);

            var all = await env.Orders.GetOrdersAsync();
            var ordered = await env.Orders.GetOrdersAsync(PurchaseOrderStatus.Ordered);
            var drafts = await env.Orders.GetOrdersAsync(PurchaseOrderStatus.Draft);

            Assert.Equal(new[] { second.Id, first.Id }, all.Select(o => o.Id).ToArray());
            Assert.Single(ordered);
            Assert.Equal(second.Id, ordered[0].Id);
            Assert.Single(drafts);
            Assert.Equal(first.Id, drafts[0].Id);
        }

        // ----------  Lieferant und offene Bestellungen ----------

        [Fact]
        public async Task SupplierArchive_WithAnOpenOrder_IsRejected()
        {
            using var env = new Env();
            var supplier = await env.AddSupplierAsync();
            await env.AddDraftAsync(supplier.Id);

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                env.Suppliers.ArchiveAsync(supplier.Id));

            Assert.Contains("offene Bestellung", ex.Message);
            Assert.NotNull(await env.Suppliers.GetByIdAsync(supplier.Id));
        }

        [Fact]
        public async Task SupplierArchive_AfterTheOrderWasCancelled_IsAllowed()
        {
            using var env = new Env();
            var supplier = await env.AddSupplierAsync();
            var order = await env.AddDraftAsync(supplier.Id);
            await env.Orders.CancelAsync(order.Id);

            var archived = await env.Suppliers.ArchiveAsync(supplier.Id);

            Assert.True(archived);
        }
        [Fact]
        public async Task AvailableProducts_ExcludeProductsInTheOrderAndArchivedOnes()
        {
            using var env = new Env();
            var supplier = await env.AddSupplierAsync();
            var order = await env.AddDraftAsync(supplier.Id);
            var inOrder = env.Db.AddProduct(quantity: 0);
            var free = env.Db.AddProduct(quantity: 0, unitPrice: 4.20m);
            env.Db.AddProduct(quantity: 0, isArchived: true);
            await env.Orders.AddLineAsync(order.Id, inOrder, 1, null);

            var choices = await env.Orders.GetAvailableProductsAsync(order.Id);

            Assert.Single(choices);
            Assert.Equal(free, choices[0].Id);
            Assert.Equal(4.20m, choices[0].UnitPrice);
        }

        [Fact]
        public async Task AvailableProducts_IgnoreTheLinesOfOtherOrders()
        {
            using var env = new Env();
            var supplier = await env.AddSupplierAsync();
            var first = await env.AddDraftAsync(supplier.Id);
            var second = await env.AddDraftAsync(supplier.Id);
            var productId = env.Db.AddProduct(quantity: 0);
            await env.Orders.AddLineAsync(first.Id, productId, 1, null);

            var choices = await env.Orders.GetAvailableProductsAsync(second.Id);

            Assert.Single(choices);
            Assert.Equal(productId, choices[0].Id);
        }
    }

}