using Microsoft.EntityFrameworkCore;
using StockManager.Data;
using StockManager.Models;
using StockManager.Services;
using StockManager.ViewModels;

namespace StockManager.Tests
{
    public class GoodsReceiptTests
    {
        private sealed record Prepared(PurchaseOrder Order, List<PurchaseOrderLine> Lines, List<int> ProductIds);

        private sealed class Env : IDisposable
        {
            public TestDatabase Db { get; } = new();
            public ApplicationDbContext Context { get; }
            public SupplierService Suppliers { get; }
            public PurchaseOrderService Orders { get; }
            public ProductService Products { get; }
            public StockService Stock { get; }

            public Env()
            {
                Context = Db.CreateContext();
                Suppliers = new SupplierService(Context);
                Orders = new PurchaseOrderService(Context);
                Products = new ProductService(Context);
                Stock = new StockService(Context);
            }

            // Legt Lieferant, Entwurf, Produkte und Positionen an und gibt die Bestellung auf.
            public async Task<Prepared> PrepareOrderedAsync(int[] quantities, int initialStock = 0)
            {
                var supplier = await Suppliers.CreateAsync(new SupplierEditViewModel
                {
                    Name = "Lieferant " + Guid.NewGuid().ToString("N")[..6]
                });
                var order = await Orders.CreateDraftAsync(new PurchaseOrderEditViewModel { SupplierId = supplier.Id });

                var lines = new List<PurchaseOrderLine>();
                var productIds = new List<int>();

                foreach (var quantity in quantities)
                {
                    var productId = Db.AddProduct(quantity: initialStock);
                    var line = await Orders.AddLineAsync(order.Id, productId, quantity, null);
                    lines.Add(line!);
                    productIds.Add(productId);
                }

                await Orders.PlaceOrderAsync(order.Id);

                return new Prepared(order, lines, productIds);
            }

            public void Dispose()
            {
                Context.Dispose();
                Db.Dispose();
            }
        }

        private static ReceiveOrderViewModel Input(
            int orderId, string? comment, params (int LineId, int? Quantity)[] lines) => new()
            {
                OrderId = orderId,
                Comment = comment,
                Lines = lines.Select(l => new ReceiveLineInput { LineId = l.LineId, QuantityNow = l.Quantity }).ToList()
            };

        // ---------- Wareneingang buchen ----------

        [Fact]
        public async Task Receive_Everything_FillsStockWritesMovementAndCompletesTheOrder()
        {
            using var env = new Env();
            var prepared = await env.PrepareOrderedAsync(new[] { 10 }, initialStock: 5);
            var line = prepared.Lines[0];

            var result = await env.Orders.ReceiveAsync(
                prepared.Order.Id, Input(prepared.Order.Id, null, (line.Id, 10)));

            Assert.NotNull(result);
            Assert.Equal(PurchaseOrderStatus.Received, result.Status);
            Assert.Equal(10, result.Units);
            Assert.Equal(15, env.Db.GetQuantity(prepared.ProductIds[0]));

            var movement = await env.Context.StockMovements.AsNoTracking().SingleAsync();
            Assert.Equal(StockMovementType.Entry, movement.MovementType);
            Assert.Equal(10, movement.QuantityChange);
            Assert.Equal(15, movement.QuantityAfter);
            Assert.Equal(line.Id, movement.PurchaseOrderLineId);
            Assert.Contains(prepared.Order.OrderNumber, movement.Comment!);

            var details = await env.Orders.GetDetailsAsync(prepared.Order.Id);
            Assert.NotNull(details);
            Assert.Equal(PurchaseOrderStatus.Received, details.Status);
            Assert.Equal(10, details.Lines[0].QuantityReceived);
        }

        [Fact]
        public async Task Receive_InTwoDeliveries_TracksStatusAndTotals()
        {
            using var env = new Env();
            var prepared = await env.PrepareOrderedAsync(new[] { 10 });
            var id = prepared.Order.Id;
            var line = prepared.Lines[0];

            var first = await env.Orders.ReceiveAsync(id, Input(id, null, (line.Id, 4)));

            Assert.NotNull(first);
            Assert.Equal(PurchaseOrderStatus.PartiallyReceived, first.Status);

            var afterFirst = await env.Orders.GetDetailsAsync(id);
            Assert.NotNull(afterFirst);
            Assert.Equal(4, afterFirst.Lines[0].QuantityReceived);
            Assert.True(afterFirst.CanReceive);
            Assert.NotNull(afterFirst.LastReceiptAt);

            var second = await env.Orders.ReceiveAsync(id, Input(id, null, (line.Id, 6)));

            Assert.NotNull(second);
            Assert.Equal(PurchaseOrderStatus.Received, second.Status);
            Assert.Equal(10, env.Db.GetQuantity(prepared.ProductIds[0]));
            Assert.Equal(2, await env.Context.StockMovements.CountAsync());

            var afterSecond = await env.Orders.GetDetailsAsync(id);
            Assert.NotNull(afterSecond);
            Assert.False(afterSecond.CanReceive);
        }

        [Fact]
        public async Task Receive_MoreThanOrdered_IsRejected_AndChangesNothing()
        {
            using var env = new Env();
            var prepared = await env.PrepareOrderedAsync(new[] { 10 });
            var id = prepared.Order.Id;

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                env.Orders.ReceiveAsync(id, Input(id, null, (prepared.Lines[0].Id, 11))));

            Assert.Equal(0, env.Db.GetQuantity(prepared.ProductIds[0]));
            Assert.Equal(0, await env.Context.StockMovements.CountAsync());

            var details = await env.Orders.GetDetailsAsync(id);
            Assert.NotNull(details);
            Assert.Equal(PurchaseOrderStatus.Ordered, details.Status);
            Assert.Equal(0, details.Lines[0].QuantityReceived);
        }

        [Fact]
        public async Task Receive_MoreThanRemainingAfterAPartialDelivery_IsRejected()
        {
            using var env = new Env();
            var prepared = await env.PrepareOrderedAsync(new[] { 10 });
            var id = prepared.Order.Id;
            var line = prepared.Lines[0];

            await env.Orders.ReceiveAsync(id, Input(id, null, (line.Id, 4)));

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                env.Orders.ReceiveAsync(id, Input(id, null, (line.Id, 7))));

            Assert.Contains("höchstens noch 6", ex.Message);
            Assert.Equal(4, env.Db.GetQuantity(prepared.ProductIds[0]));
        }

        [Fact]
        public async Task Receive_WithAllQuantitiesZero_IsRejected()
        {
            using var env = new Env();
            var prepared = await env.PrepareOrderedAsync(new[] { 10 });
            var id = prepared.Order.Id;

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                env.Orders.ReceiveAsync(id, Input(id, null, (prepared.Lines[0].Id, 0))));

            Assert.Contains("mindestens", ex.Message);
            Assert.Equal(0, await env.Context.StockMovements.CountAsync());
        }

        [Fact]
        public async Task Receive_NegativeQuantity_IsRejected()
        {
            using var env = new Env();
            var prepared = await env.PrepareOrderedAsync(new[] { 10 });
            var id = prepared.Order.Id;

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                env.Orders.ReceiveAsync(id, Input(id, null, (prepared.Lines[0].Id, -1))));

            Assert.Equal(0, env.Db.GetQuantity(prepared.ProductIds[0]));
        }

        // ---------- Statusregeln ----------

        [Fact]
        public async Task Receive_OnDraftOrder_IsRejected()
        {
            using var env = new Env();
            var supplier = await env.Suppliers.CreateAsync(new SupplierEditViewModel { Name = "Alpha GmbH" });
            var draft = await env.Orders.CreateDraftAsync(new PurchaseOrderEditViewModel { SupplierId = supplier.Id });
            var productId = env.Db.AddProduct(quantity: 0);
            var line = await env.Orders.AddLineAsync(draft.Id, productId, 3, null);
            Assert.NotNull(line);

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                env.Orders.ReceiveAsync(draft.Id, Input(draft.Id, null, (line.Id, 1))));

            Assert.Contains("Entwurf", ex.Message);
            Assert.Equal(0, env.Db.GetQuantity(productId));
        }

        [Fact]
        public async Task Receive_OnCancelledOrder_IsRejected()
        {
            using var env = new Env();
            var prepared = await env.PrepareOrderedAsync(new[] { 5 });
            var id = prepared.Order.Id;
            await env.Orders.CancelAsync(id);

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                env.Orders.ReceiveAsync(id, Input(id, null, (prepared.Lines[0].Id, 1))));

            Assert.Contains("Storniert", ex.Message);
        }

        [Fact]
        public async Task Receive_OnCompletedOrder_IsRejected()
        {
            using var env = new Env();
            var prepared = await env.PrepareOrderedAsync(new[] { 5 });
            var id = prepared.Order.Id;
            var line = prepared.Lines[0];
            await env.Orders.ReceiveAsync(id, Input(id, null, (line.Id, 5)));

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                env.Orders.ReceiveAsync(id, Input(id, null, (line.Id, 1))));

            Assert.Contains("Geliefert", ex.Message);
            Assert.Equal(5, env.Db.GetQuantity(prepared.ProductIds[0]));
        }

        [Fact]
        public async Task Receive_UnknownOrder_ReturnsNull()
        {
            using var env = new Env();

            var result = await env.Orders.ReceiveAsync(9999, Input(9999, null));

            Assert.Null(result);
        }

        [Fact]
        public async Task Cancel_AfterAReceipt_IsRejected()
        {
            using var env = new Env();
            var prepared = await env.PrepareOrderedAsync(new[] { 10 });
            var id = prepared.Order.Id;
            await env.Orders.ReceiveAsync(id, Input(id, null, (prepared.Lines[0].Id, 3)));

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                env.Orders.CancelAsync(id));

            Assert.Contains("Wareneingang", ex.Message);
        }

        // ---------- Positionen und Eingaben ----------

        [Fact]
        public async Task Receive_LineOfAnotherOrder_IsRejected()
        {
            using var env = new Env();
            var first = await env.PrepareOrderedAsync(new[] { 5 });
            var second = await env.PrepareOrderedAsync(new[] { 5 });

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                env.Orders.ReceiveAsync(first.Order.Id,
                    Input(first.Order.Id, null, (second.Lines[0].Id, 1))));

            Assert.Equal(0, env.Db.GetQuantity(second.ProductIds[0]));
        }

        [Fact]
        public async Task Receive_SameLineTwice_IsRejected()
        {
            using var env = new Env();
            var prepared = await env.PrepareOrderedAsync(new[] { 10 });
            var id = prepared.Order.Id;
            var line = prepared.Lines[0];

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                env.Orders.ReceiveAsync(id, Input(id, null, (line.Id, 1), (line.Id, 1))));

            Assert.Equal(0, env.Db.GetQuantity(prepared.ProductIds[0]));
        }

        [Fact]
        public async Task Receive_OnlyTheLinesWithAQuantity_AreBooked()
        {
            using var env = new Env();
            var prepared = await env.PrepareOrderedAsync(new[] { 5, 8 });
            var id = prepared.Order.Id;

            var result = await env.Orders.ReceiveAsync(id,
                Input(id, null, (prepared.Lines[0].Id, 5), (prepared.Lines[1].Id, 0)));

            Assert.NotNull(result);
            Assert.Equal(PurchaseOrderStatus.PartiallyReceived, result.Status);
            Assert.Equal(1, result.LineCount);
            Assert.Equal(5, env.Db.GetQuantity(prepared.ProductIds[0]));
            Assert.Equal(0, env.Db.GetQuantity(prepared.ProductIds[1]));
            Assert.Equal(1, await env.Context.StockMovements.CountAsync());
        }

        [Fact]
        public async Task Receive_Comment_IsStoredOnTheMovement()
        {
            using var env = new Env();
            var prepared = await env.PrepareOrderedAsync(new[] { 5 });
            var id = prepared.Order.Id;

            await env.Orders.ReceiveAsync(id,
                Input(id, "  Lieferschein 4711  ", (prepared.Lines[0].Id, 5)));

            var movement = await env.Context.StockMovements.AsNoTracking().SingleAsync();
            Assert.Contains("Lieferschein 4711", movement.Comment!);
            Assert.Contains(prepared.Order.OrderNumber, movement.Comment!);
        }

        [Fact]
        public async Task Receive_CommentTooLong_IsRejected()
        {
            using var env = new Env();
            var prepared = await env.PrepareOrderedAsync(new[] { 5 });
            var id = prepared.Order.Id;

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                env.Orders.ReceiveAsync(id,
                    Input(id, new string('x', 201), (prepared.Lines[0].Id, 5))));

            Assert.Equal(0, env.Db.GetQuantity(prepared.ProductIds[0]));
        }

        [Fact]
        public async Task Receive_ArchivedProduct_IsRejected_AndChangesNothing()
        {
            using var env = new Env();
            var prepared = await env.PrepareOrderedAsync(new[] { 5 });
            var id = prepared.Order.Id;

            // Archivierung direkt in der Datenbank (die Regel würde sie verhindern)
            var product = await env.Context.Products.SingleAsync(p => p.Id == prepared.ProductIds[0]);
            product.IsArchived = true;
            await env.Context.SaveChangesAsync();

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                env.Orders.ReceiveAsync(id, Input(id, null, (prepared.Lines[0].Id, 5))));

            Assert.Contains("archiviert", ex.Message);
            Assert.Equal(0, env.Db.GetQuantity(prepared.ProductIds[0]));
        }

        // ---------- Formular und Historie ----------

        [Fact]
        public async Task ReceiptForm_ListsOnlyOpenLines_WithTheRemainderAsDefault()
        {
            using var env = new Env();
            var prepared = await env.PrepareOrderedAsync(new[] { 5, 8 });
            var id = prepared.Order.Id;

            await env.Orders.ReceiveAsync(id, Input(id, null, (prepared.Lines[0].Id, 5)));
            var afterFirst = await env.Orders.GetReceiptFormAsync(id);

            Assert.NotNull(afterFirst);
            Assert.Single(afterFirst.Lines);
            Assert.Equal(prepared.Lines[1].Id, afterFirst.Lines[0].LineId);
            Assert.Equal(8, afterFirst.Lines[0].QuantityNow);

            await env.Orders.ReceiveAsync(id, Input(id, null, (prepared.Lines[1].Id, 3)));
            var afterSecond = await env.Orders.GetReceiptFormAsync(id);

            Assert.NotNull(afterSecond);
            Assert.Single(afterSecond.Lines);
            Assert.Equal(3, afterSecond.Lines[0].QuantityReceived);
            Assert.Equal(5, afterSecond.Lines[0].Remaining);
            Assert.Equal(5, afterSecond.Lines[0].QuantityNow);
        }

        [Fact]
        public async Task ReceiptForm_ForADraftOrAnUnknownOrder_ReturnsNull()
        {
            using var env = new Env();
            var supplier = await env.Suppliers.CreateAsync(new SupplierEditViewModel { Name = "Alpha GmbH" });
            var draft = await env.Orders.CreateDraftAsync(new PurchaseOrderEditViewModel { SupplierId = supplier.Id });

            Assert.Null(await env.Orders.GetReceiptFormAsync(draft.Id));
            Assert.Null(await env.Orders.GetReceiptFormAsync(9999));
        }

        [Fact]
        public async Task StockHistory_ShowsTheReceiptMovement()
        {
            using var env = new Env();
            var prepared = await env.PrepareOrderedAsync(new[] { 5 });
            var id = prepared.Order.Id;
            var line = prepared.Lines[0];
            await env.Orders.ReceiveAsync(id, Input(id, null, (line.Id, 5)));

            var history = await env.Stock.GetHistoryAsync(prepared.ProductIds[0], page: 1);

            Assert.NotNull(history);
            Assert.Equal(1, history.TotalMovements);
            Assert.Equal(line.Id, history.Movements[0].PurchaseOrderLineId);
            Assert.Equal(StockMovementType.Entry, history.Movements[0].MovementType);
        }

        // ---------- Produkt archivieren ----------

        [Fact]
        public async Task ProductArchive_WithoutOrders_Works()
        {
            using var env = new Env();
            var productId = env.Db.AddProduct(quantity: 0);

            var archived = await env.Products.ArchiveProductAsync(productId);

            Assert.True(archived);
        }

        [Fact]
        public async Task ProductArchive_WithAnOpenOrder_IsRejected()
        {
            using var env = new Env();
            var supplier = await env.Suppliers.CreateAsync(new SupplierEditViewModel { Name = "Alpha GmbH" });
            var draft = await env.Orders.CreateDraftAsync(new PurchaseOrderEditViewModel { SupplierId = supplier.Id });
            var productId = env.Db.AddProduct(quantity: 0);
            await env.Orders.AddLineAsync(draft.Id, productId, 3, null);

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                env.Products.ArchiveProductAsync(productId));

            Assert.Contains("offenen Bestellung", ex.Message);
            Assert.NotNull(await env.Products.GetProductByIdAsync(productId));
        }

        [Fact]
        public async Task ProductArchive_NamesTheNumberOfOpenOrders()
        {
            using var env = new Env();
            var supplier = await env.Suppliers.CreateAsync(new SupplierEditViewModel { Name = "Alpha GmbH" });
            var first = await env.Orders.CreateDraftAsync(new PurchaseOrderEditViewModel { SupplierId = supplier.Id });
            var second = await env.Orders.CreateDraftAsync(new PurchaseOrderEditViewModel { SupplierId = supplier.Id });
            var productId = env.Db.AddProduct(quantity: 0);
            await env.Orders.AddLineAsync(first.Id, productId, 1, null);
            await env.Orders.AddLineAsync(second.Id, productId, 1, null);

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                env.Products.ArchiveProductAsync(productId));

            Assert.Contains("2 offenen Bestellungen", ex.Message);
        }

        [Fact]
        public async Task ProductArchive_AfterTheOrderIsCompletedOrCancelled_IsAllowed()
        {
            using var env = new Env();
            var cancelled = await env.PrepareOrderedAsync(new[] { 5 });
            await env.Orders.CancelAsync(cancelled.Order.Id);

            var completed = await env.PrepareOrderedAsync(new[] { 5 });
            await env.Orders.ReceiveAsync(completed.Order.Id,
                Input(completed.Order.Id, null, (completed.Lines[0].Id, 5)));

            Assert.True(await env.Products.ArchiveProductAsync(cancelled.ProductIds[0]));
            Assert.True(await env.Products.ArchiveProductAsync(completed.ProductIds[0]));
        }
        // ---------- Teillieferung abschließen ----------

        [Fact]
        public async Task Close_AfterAPartialDelivery_ClosesTheOrder_AndKeepsTheStock()
        {
            using var env = new Env();
            var prepared = await env.PrepareOrderedAsync(new[] { 10 });
            var id = prepared.Order.Id;
            await env.Orders.ReceiveAsync(id, Input(id, null, (prepared.Lines[0].Id, 4)));

            var closed = await env.Orders.CloseAsync(id);
            var details = await env.Orders.GetDetailsAsync(id);

            Assert.True(closed);
            Assert.NotNull(details);
            Assert.Equal(PurchaseOrderStatus.Closed, details.Status);
            Assert.Equal("Abgeschlossen (unvollständig)", details.Status.GetDisplayName());
            Assert.Equal(4, env.Db.GetQuantity(prepared.ProductIds[0]));
            Assert.Equal(4, details.Lines[0].QuantityReceived);
            Assert.False(details.CanReceive);
            Assert.False(details.CanClose);
            Assert.False(details.CanCancel);
            Assert.False(details.CanEdit);
        }

        [Fact]
        public async Task Close_Twice_IsRejected()
        {
            using var env = new Env();
            var prepared = await env.PrepareOrderedAsync(new[] { 10 });
            var id = prepared.Order.Id;
            await env.Orders.ReceiveAsync(id, Input(id, null, (prepared.Lines[0].Id, 4)));
            await env.Orders.CloseAsync(id);

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                env.Orders.CloseAsync(id));

            Assert.Contains("bereits abgeschlossen", ex.Message);
        }

        [Fact]
        public async Task Close_WithoutAnyReceipt_IsRejected_ButCancelWorks()
        {
            using var env = new Env();
            var prepared = await env.PrepareOrderedAsync(new[] { 10 });
            var id = prepared.Order.Id;

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                env.Orders.CloseAsync(id));

            Assert.Contains("stornieren", ex.Message);
            Assert.True(await env.Orders.CancelAsync(id));
        }

        [Fact]
        public async Task Close_FullyReceivedOrder_IsRejected()
        {
            using var env = new Env();
            var prepared = await env.PrepareOrderedAsync(new[] { 5 });
            var id = prepared.Order.Id;
            await env.Orders.ReceiveAsync(id, Input(id, null, (prepared.Lines[0].Id, 5)));

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                env.Orders.CloseAsync(id));

            Assert.Contains("vollständig geliefert", ex.Message);
        }

        [Fact]
        public async Task Close_CancelledOrder_IsRejected()
        {
            using var env = new Env();
            var prepared = await env.PrepareOrderedAsync(new[] { 5 });
            var id = prepared.Order.Id;
            await env.Orders.CancelAsync(id);

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                env.Orders.CloseAsync(id));

            Assert.Contains("stornierte", ex.Message);
        }

        [Fact]
        public async Task Close_DraftOrder_IsRejected()
        {
            using var env = new Env();
            var supplier = await env.Suppliers.CreateAsync(new SupplierEditViewModel { Name = "Alpha GmbH" });
            var draft = await env.Orders.CreateDraftAsync(new PurchaseOrderEditViewModel { SupplierId = supplier.Id });

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                env.Orders.CloseAsync(draft.Id));

            Assert.Contains("teilweise gelieferte", ex.Message);
        }

        [Fact]
        public async Task Close_UnknownOrder_ReturnsFalse()
        {
            using var env = new Env();

            Assert.False(await env.Orders.CloseAsync(9999));
        }

        [Fact]
        public async Task Receive_AfterClosing_IsRejected()
        {
            using var env = new Env();
            var prepared = await env.PrepareOrderedAsync(new[] { 10 });
            var id = prepared.Order.Id;
            var line = prepared.Lines[0];
            await env.Orders.ReceiveAsync(id, Input(id, null, (line.Id, 4)));
            await env.Orders.CloseAsync(id);

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                env.Orders.ReceiveAsync(id, Input(id, null, (line.Id, 1))));

            Assert.Contains("Abgeschlossen", ex.Message);
            Assert.Equal(4, env.Db.GetQuantity(prepared.ProductIds[0]));
            Assert.Null(await env.Orders.GetReceiptFormAsync(id));
        }

        [Fact]
        public async Task Close_ReleasesTheSupplierAndTheProductsForArchiving()
        {
            using var env = new Env();
            var prepared = await env.PrepareOrderedAsync(new[] { 10 });
            var id = prepared.Order.Id;
            await env.Orders.ReceiveAsync(id, Input(id, null, (prepared.Lines[0].Id, 4)));

            // Solange die Bestellung offen ist, sind beide gesperrt.
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                env.Suppliers.ArchiveAsync(prepared.Order.SupplierId));
            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                env.Products.ArchiveProductAsync(prepared.ProductIds[0]));

            await env.Orders.CloseAsync(id);

            Assert.True(await env.Suppliers.ArchiveAsync(prepared.Order.SupplierId));
            Assert.True(await env.Products.ArchiveProductAsync(prepared.ProductIds[0]));
        }

        [Fact]
        public async Task ClosedOrders_AppearUnderTheirOwnFilter()
        {
            using var env = new Env();
            var closed = await env.PrepareOrderedAsync(new[] { 10 });
            await env.Orders.ReceiveAsync(closed.Order.Id,
                Input(closed.Order.Id, null, (closed.Lines[0].Id, 4)));
            await env.Orders.CloseAsync(closed.Order.Id);
            await env.PrepareOrderedAsync(new[] { 3 });

            var list = await env.Orders.GetOrdersAsync(PurchaseOrderStatus.Closed);

            Assert.Single(list);
            Assert.Equal(closed.Order.Id, list[0].Id);
            Assert.Equal(PurchaseOrderStatus.Closed, list[0].Status);
        }
    }
}