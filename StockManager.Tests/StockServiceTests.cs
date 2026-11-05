using StockManager.Models;
using StockManager.Services;
using Xunit;

namespace StockManager.Tests
{

    public class StockServiceTests
    {
        // ---------- Eingang ----------

        [Fact]
        public async Task Entry_IncreasesStock_AndWritesJournalEntry()
        {
            // Arrange: Datenbank und Produkt mit Bestand 5
            using var db = new TestDatabase();
            var productId = db.AddProduct(quantity: 5);
            using var context = db.CreateContext();
            var service = new StockService(context);

            // Act: Wareneingang von 10
            var movement = await service.RecordMovementAsync(
                productId, StockMovementType.Entry, 10, null);

            // Assert: Bestand, Journalzeile und Datenbank stimmen überein
            Assert.NotNull(movement);
            Assert.Equal(10, movement.QuantityChange);
            Assert.Equal(15, movement.QuantityAfter);
            Assert.Equal(15, db.GetQuantity(productId));
            Assert.Equal(1, db.CountMovements(productId));
        }

        [Fact]
        public async Task Entry_WouldOverflow_IsRejected()
        {
            using var db = new TestDatabase();
            var productId = db.AddProduct(quantity: int.MaxValue);
            using var context = db.CreateContext();
            var service = new StockService(context);

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                service.RecordMovementAsync(productId, StockMovementType.Entry, 1, null));

            Assert.Equal(int.MaxValue, db.GetQuantity(productId));
            Assert.Equal(0, db.CountMovements(productId));
        }

        // ---------- Ausgang ----------

        [Fact]
        public async Task Exit_DecreasesStock_AndWritesJournalEntry()
        {
            using var db = new TestDatabase();
            var productId = db.AddProduct(quantity: 10);
            using var context = db.CreateContext();
            var service = new StockService(context);

            var movement = await service.RecordMovementAsync(
                productId, StockMovementType.Exit, 3, null);

            Assert.NotNull(movement);
            Assert.Equal(-3, movement.QuantityChange);
            Assert.Equal(7, movement.QuantityAfter);
            Assert.Equal(7, db.GetQuantity(productId));
        }

        [Fact]
        public async Task Exit_MoreThanAvailable_IsRejected_AndChangesNothing()
        {
            using var db = new TestDatabase();
            var productId = db.AddProduct(quantity: 5);
            using var context = db.CreateContext();
            var service = new StockService(context);

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                service.RecordMovementAsync(productId, StockMovementType.Exit, 8, null));

            Assert.Contains("verfügbar sind 5", ex.Message);
            Assert.Equal(5, db.GetQuantity(productId));
            Assert.Equal(0, db.CountMovements(productId));
        }

        [Fact]
        public async Task Exit_ExactlyTheWholeStock_IsAllowed()
        {
            using var db = new TestDatabase();
            var productId = db.AddProduct(quantity: 5);
            using var context = db.CreateContext();
            var service = new StockService(context);

            var movement = await service.RecordMovementAsync(
                productId, StockMovementType.Exit, 5, null);

            Assert.NotNull(movement);
            Assert.Equal(0, movement.QuantityAfter);
            Assert.Equal(0, db.GetQuantity(productId));
        }

        // ---------- Inventurkorrektur ----------

        [Fact]
        public async Task Adjustment_Upwards_StoresThePositiveDifference()
        {
            using var db = new TestDatabase();
            var productId = db.AddProduct(quantity: 7);
            using var context = db.CreateContext();
            var service = new StockService(context);

            var movement = await service.RecordMovementAsync(
                productId, StockMovementType.Adjustment, 12, null);

            Assert.NotNull(movement);
            Assert.Equal(5, movement.QuantityChange);
            Assert.Equal(12, movement.QuantityAfter);
            Assert.Equal(12, db.GetQuantity(productId));
        }

        [Fact]
        public async Task Adjustment_Downwards_StoresTheNegativeDifference()
        {
            using var db = new TestDatabase();
            var productId = db.AddProduct(quantity: 7);
            using var context = db.CreateContext();
            var service = new StockService(context);

            var movement = await service.RecordMovementAsync(
                productId, StockMovementType.Adjustment, 3, null);

            Assert.NotNull(movement);
            Assert.Equal(-4, movement.QuantityChange);
            Assert.Equal(3, movement.QuantityAfter);
        }

        [Fact]
        public async Task Adjustment_ToTheSameQuantity_IsRejected()
        {
            using var db = new TestDatabase();
            var productId = db.AddProduct(quantity: 7);
            using var context = db.CreateContext();
            var service = new StockService(context);

            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                service.RecordMovementAsync(productId, StockMovementType.Adjustment, 7, null));

            Assert.Contains("beträgt bereits 7", ex.Message);
            Assert.Equal(0, db.CountMovements(productId));
        }

        [Fact]
        public async Task Adjustment_ToZero_IsAllowed()
        {
            using var db = new TestDatabase();
            var productId = db.AddProduct(quantity: 7);
            using var context = db.CreateContext();
            var service = new StockService(context);

            var movement = await service.RecordMovementAsync(
                productId, StockMovementType.Adjustment, 0, null);

            Assert.NotNull(movement);
            Assert.Equal(-7, movement.QuantityChange);
            Assert.Equal(0, db.GetQuantity(productId));
        }

        [Fact]
        public async Task Adjustment_WithNegativeCount_IsRejected()
        {
            using var db = new TestDatabase();
            var productId = db.AddProduct(quantity: 7);
            using var context = db.CreateContext();
            var service = new StockService(context);

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                service.RecordMovementAsync(productId, StockMovementType.Adjustment, -1, null));

            Assert.Equal(7, db.GetQuantity(productId));
        }

        // ---------- Ungültige Mengen ----------

        [Theory]
        [InlineData(StockMovementType.Entry, 0)]
        [InlineData(StockMovementType.Entry, -1)]
        [InlineData(StockMovementType.Exit, 0)]
        [InlineData(StockMovementType.Exit, -1)]
        public async Task EntryOrExit_WithQuantityBelowOne_IsRejected(
            StockMovementType type, int quantity)
        {
            using var db = new TestDatabase();
            var productId = db.AddProduct(quantity: 5);
            using var context = db.CreateContext();
            var service = new StockService(context);

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                service.RecordMovementAsync(productId, type, quantity, null));

            Assert.Equal(5, db.GetQuantity(productId));
            Assert.Equal(0, db.CountMovements(productId));
        }

        // ---------- Produkt nicht gefunden ----------

        [Fact]
        public async Task UnknownProduct_ReturnsNull()
        {
            using var db = new TestDatabase();
            using var context = db.CreateContext();
            var service = new StockService(context);

            var movement = await service.RecordMovementAsync(
                9999, StockMovementType.Entry, 1, null);

            Assert.Null(movement);
        }

        [Fact]
        public async Task ArchivedProduct_ReturnsNull_AndChangesNothing()
        {
            using var db = new TestDatabase();
            var productId = db.AddProduct(quantity: 5, isArchived: true);
            using var context = db.CreateContext();
            var service = new StockService(context);

            var movement = await service.RecordMovementAsync(
                productId, StockMovementType.Entry, 1, null);

            Assert.Null(movement);
            Assert.Equal(5, db.GetQuantity(productId));
            Assert.Equal(0, db.CountMovements(productId));
        }

        // ---------- Kommentar ----------

        [Fact]
        public async Task Comment_IsTrimmed()
        {
            using var db = new TestDatabase();
            var productId = db.AddProduct(quantity: 5);
            using var context = db.CreateContext();
            var service = new StockService(context);

            var movement = await service.RecordMovementAsync(
                productId, StockMovementType.Entry, 1, "  Inventur Q4  ");

            Assert.NotNull(movement);
            Assert.Equal("Inventur Q4", movement.Comment);
        }

        [Fact]
        public async Task Comment_Blank_IsStoredAsNull()
        {
            using var db = new TestDatabase();
            var productId = db.AddProduct(quantity: 5);
            using var context = db.CreateContext();
            var service = new StockService(context);

            var movement = await service.RecordMovementAsync(
                productId, StockMovementType.Entry, 1, "   ");

            Assert.NotNull(movement);
            Assert.Null(movement.Comment);
        }

        [Fact]
        public async Task Comment_TooLong_IsRejected()
        {
            using var db = new TestDatabase();
            var productId = db.AddProduct(quantity: 5);
            using var context = db.CreateContext();
            var service = new StockService(context);

            await Assert.ThrowsAsync<InvalidOperationException>(() =>
                service.RecordMovementAsync(
                    productId, StockMovementType.Entry, 1, new string('x', 251)));

            Assert.Equal(0, db.CountMovements(productId));
        }

        // ---------- Historie ----------

        [Fact]
        public async Task History_ListsNewestFirst()
        {
            using var db = new TestDatabase();
            var productId = db.AddProduct(quantity: 0);
            using var context = db.CreateContext();
            var service = new StockService(context);

            await service.RecordMovementAsync(productId, StockMovementType.Entry, 1, null);
            await service.RecordMovementAsync(productId, StockMovementType.Entry, 2, null);
            await service.RecordMovementAsync(productId, StockMovementType.Entry, 3, null);

            var history = await service.GetHistoryAsync(productId, page: 1);

            Assert.NotNull(history);
            Assert.Equal(3, history.TotalMovements);
            Assert.Equal(
                new[] { 3, 2, 1 },
                history.Movements.Select(m => m.QuantityChange).ToArray());
        }

        [Fact]
        public async Task History_Paginates_AndClampsTheRequestedPage()
        {
            using var db = new TestDatabase();
            var productId = db.AddProduct(quantity: 0);
            using var context = db.CreateContext();
            var service = new StockService(context);

            await service.RecordMovementAsync(productId, StockMovementType.Entry, 1, null);
            await service.RecordMovementAsync(productId, StockMovementType.Entry, 2, null);
            await service.RecordMovementAsync(productId, StockMovementType.Entry, 3, null);

            var secondPage = await service.GetHistoryAsync(productId, page: 2, pageSize: 2);
            var tooFar = await service.GetHistoryAsync(productId, page: 999, pageSize: 2);

            Assert.NotNull(secondPage);
            Assert.Equal(2, secondPage.TotalPages);
            Assert.Single(secondPage.Movements);

            Assert.NotNull(tooFar);
            Assert.Equal(2, tooFar.CurrentPage);
        }

        [Fact]
        public async Task History_ForUnknownProduct_ReturnsNull()
        {
            using var db = new TestDatabase();
            using var context = db.CreateContext();
            var service = new StockService(context);

            var history = await service.GetHistoryAsync(9999, page: 1);

            Assert.Null(history);
        }
    }
}