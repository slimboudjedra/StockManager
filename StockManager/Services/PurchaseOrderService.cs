using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using StockManager.Data;
using StockManager.Models;
using StockManager.ViewModels;

namespace StockManager.Services
{
    public class PurchaseOrderService
    {
        private const int MaxNumberAttempts = 3;

        private const string SupplierUnusableMessage =
            "Der Lieferant existiert nicht oder ist archiviert.";

        private const string AlreadyInOrderMessage =
            "Dieses Produkt ist bereits in der Bestellung. Ändern Sie dort die Menge.";
        private const int MaxReceiptCommentLength = 200;

        private readonly ApplicationDbContext _context;

        public PurchaseOrderService(ApplicationDbContext context)
        {
            _context = context;
        }

        // ------------------------------------------------------------------ Lesen

        public async Task<List<PurchaseOrderListItem>> GetOrdersAsync(PurchaseOrderStatus? status = null)
        {
            var query = _context.PurchaseOrders.AsNoTracking();

            if (status != null)
            {
                query = query.Where(o => o.Status == status.Value);
            }

            var orders = await query
                .Include(o => o.Supplier)
                .Include(o => o.Lines)
                .OrderByDescending(o => o.CreatedAt)
                .ThenByDescending(o => o.Id)
                .ToListAsync();

            // Die Summe wird im Speicher berechnet (SQLite kann decimal nicht in SQL addieren).
            return orders
                .Select(o => new PurchaseOrderListItem
                {
                    Id = o.Id,
                    OrderNumber = o.OrderNumber,
                    SupplierName = o.Supplier.Name,
                    Status = o.Status,
                    CreatedAt = o.CreatedAt,
                    ExpectedDeliveryDate = o.ExpectedDeliveryDate,
                    LineCount = o.Lines.Count,
                    TotalAmount = o.Lines.Sum(l => l.QuantityOrdered * l.PurchasePrice)
                })
                .ToList();
        }

        //---------------------------------------------------------------------------------------------------------//

        public async Task<PurchaseOrderDetailsViewModel?> GetDetailsAsync(int id)
        {
            var order = await _context.PurchaseOrders
                .AsNoTracking()
                .Include(o => o.Supplier)
                .Include(o => o.Lines).ThenInclude(l => l.Product)
                .FirstOrDefaultAsync(o => o.Id == id);

            if (order == null)
            {
                return null;
            }

            return new PurchaseOrderDetailsViewModel
            {
                Id = order.Id,
                OrderNumber = order.OrderNumber,
                SupplierId = order.SupplierId,
                SupplierName = order.Supplier.Name,
                Status = order.Status,
                CreatedAt = order.CreatedAt,
                OrderedAt = order.OrderedAt,
                ExpectedDeliveryDate = order.ExpectedDeliveryDate,
                Notes = order.Notes,
                LastReceiptAt = order.LastReceiptAt,
                Lines = order.Lines
                    .OrderBy(l => l.Product.Name)
                    .Select(l => new PurchaseOrderLineItem
                    {
                        Id = l.Id,
                        ProductId = l.ProductId,
                        ProductReference = l.Product.Reference,
                        ProductName = l.Product.Name,
                        QuantityOrdered = l.QuantityOrdered,
                        QuantityReceived = l.QuantityReceived,
                        PurchasePrice = l.PurchasePrice
                    })
                    .ToList()
            };
        }

        //---------------------------------------------------------------------------------------------------------//

        // Gibt nur Entwürfe zurück: Alles andere darf nicht mehr bearbeitet werden.
        public async Task<PurchaseOrderEditViewModel?> GetForEditAsync(int id)
        {
            return await _context.PurchaseOrders
                .AsNoTracking()
                .Where(o => o.Id == id && o.Status == PurchaseOrderStatus.Draft)
                .Select(o => new PurchaseOrderEditViewModel
                {
                    Id = o.Id,
                    SupplierId = o.SupplierId,
                    ExpectedDeliveryDate = o.ExpectedDeliveryDate,
                    Notes = o.Notes
                })
                .FirstOrDefaultAsync();
        }

        // ---------------------------------------------------------------- Entwurf

        // Wirft InvalidOperationException, wenn der Lieferant fehlt oder archiviert ist.
        public async Task<PurchaseOrder> CreateDraftAsync(PurchaseOrderEditViewModel input)
        {
            await EnsureActiveSupplierAsync(input.SupplierId);

            var year = DateTime.UtcNow.Year;

            for (var attempt = 1; attempt <= MaxNumberAttempts; attempt++)
            {
                var order = new PurchaseOrder
                {
                    OrderNumber = await NextOrderNumberAsync(year),
                    SupplierId = input.SupplierId,
                    Status = PurchaseOrderStatus.Draft,
                    CreatedAt = DateTime.UtcNow,
                    ExpectedDeliveryDate = input.ExpectedDeliveryDate,
                    Notes = Clean(input.Notes)
                };

                _context.PurchaseOrders.Add(order);

                try
                {
                    await _context.SaveChangesAsync();
                    return order;
                }
                catch (DbUpdateException ex) when (IsDuplicateKey(ex) && attempt < MaxNumberAttempts)
                {
                    // Zwei Bestellungen gleichzeitig: Die Nummer war schon vergeben, neu versuchen.
                    _context.Entry(order).State = EntityState.Detached;
                }
            }

            throw new InvalidOperationException(
                "Die Bestellnummer konnte nicht vergeben werden. Bitte versuchen Sie es erneut.");
        }

        //---------------------------------------------------------------------------------------------------------//

        // Legt einen Entwurf samt Positionen in EINEM Schritt an (für den Nachbestellvorschlag).
        // Wirft InvalidOperationException bei verletzten Geschäftsregeln; dann wird nichts gespeichert.
        public async Task<PurchaseOrder> CreateDraftWithLinesAsync(
            int supplierId, IReadOnlyList<DraftLineInput> lines, string? notes)
        {
            await EnsureActiveSupplierAsync(supplierId);

            if (lines.Count == 0)
            {
                throw new InvalidOperationException("Eine Bestellung braucht mindestens eine Position.");
            }

            if (lines.GroupBy(l => l.ProductId).Any(g => g.Count() > 1))
            {
                throw new InvalidOperationException("Ein Produkt darf nur einmal in einer Bestellung vorkommen.");
            }

            foreach (var line in lines)
            {
                RequireValidQuantity(line.Quantity);
                RequireValidPrice(line.PurchasePrice);
            }

            var productIds = lines.Select(l => l.ProductId).ToList();

            var usableCount = await _context.Products
                .CountAsync(p => productIds.Contains(p.Id) && !p.IsArchived);

            if (usableCount != productIds.Count)
            {
                throw new InvalidOperationException(
                    "Mindestens ein Produkt existiert nicht oder ist archiviert.");
            }

            var year = DateTime.UtcNow.Year;

            for (var attempt = 1; attempt <= MaxNumberAttempts; attempt++)
            {
                var order = new PurchaseOrder
                {
                    OrderNumber = await NextOrderNumberAsync(year),
                    SupplierId = supplierId,
                    Status = PurchaseOrderStatus.Draft,
                    CreatedAt = DateTime.UtcNow,
                    Notes = Clean(notes),
                    Lines = lines
                        .Select(l => new PurchaseOrderLine
                        {
                            ProductId = l.ProductId,
                            QuantityOrdered = l.Quantity,
                            QuantityReceived = 0,
                            PurchasePrice = l.PurchasePrice
                        })
                        .ToList()
                };

                _context.PurchaseOrders.Add(order);

                try
                {
                    await _context.SaveChangesAsync();
                    return order;
                }
                catch (DbUpdateException ex) when (IsDuplicateKey(ex) && attempt < MaxNumberAttempts)
                {
                    // Zwei Bestellungen gleichzeitig: Die Nummer war schon vergeben, neu versuchen.
                    foreach (var line in order.Lines)
                    {
                        _context.Entry(line).State = EntityState.Detached;
                    }

                    _context.Entry(order).State = EntityState.Detached;
                }
            }

            throw new InvalidOperationException(
                "Die Bestellnummer konnte nicht vergeben werden. Bitte versuchen Sie es erneut.");
        }

        // Gibt false zurück, wenn die Bestellung nicht existiert.
        // Wirft InvalidOperationException, wenn sie kein Entwurf mehr ist.
        public async Task<bool> UpdateHeaderAsync(int id, PurchaseOrderEditViewModel input)
        {
            var order = await _context.PurchaseOrders.FirstOrDefaultAsync(o => o.Id == id);

            if (order == null)
            {
                return false;
            }

            RequireDraft(order);

            if (input.SupplierId != order.SupplierId)
            {
                await EnsureActiveSupplierAsync(input.SupplierId);
            }

            order.SupplierId = input.SupplierId;
            order.ExpectedDeliveryDate = input.ExpectedDeliveryDate;
            order.Notes = Clean(input.Notes);

            await _context.SaveChangesAsync();

            return true;
        }

        //---------------------------------------------------------------------------------------------------------//

        // Gibt false zurück, wenn die Bestellung nicht existiert.
        // Wirft InvalidOperationException, wenn sie kein Entwurf mehr ist.
        public async Task<bool> DeleteDraftAsync(int id)
        {
            var order = await _context.PurchaseOrders.FirstOrDefaultAsync(o => o.Id == id);

            if (order == null)
            {
                return false;
            }

            RequireDraft(order);

            _context.PurchaseOrders.Remove(order);

            await _context.SaveChangesAsync();

            return true;
        }

        // ------------------------------------------------------------ Positionen

        // Gibt null zurück, wenn die Bestellung nicht existiert.
        // Ohne Preis wird der Einzelpreis des Produkts vorgeschlagen und auf der Zeile gespeichert.
        public async Task<PurchaseOrderLine?> AddLineAsync(
            int orderId, int productId, int quantity, decimal? purchasePrice)
        {
            var order = await _context.PurchaseOrders.FirstOrDefaultAsync(o => o.Id == orderId);

            if (order == null)
            {
                return null;
            }

            RequireDraft(order);
            RequireValidQuantity(quantity);

            var product = await _context.Products
                .FirstOrDefaultAsync(p => p.Id == productId && !p.IsArchived);

            if (product == null)
            {
                throw new InvalidOperationException("Das Produkt existiert nicht oder ist archiviert.");
            }

            var alreadyInOrder = await _context.PurchaseOrderLines
                .AnyAsync(l => l.PurchaseOrderId == orderId && l.ProductId == productId);

            if (alreadyInOrder)
            {
                throw new InvalidOperationException(AlreadyInOrderMessage);
            }

            var price = purchasePrice ?? product.UnitPrice;

            RequireValidPrice(price);

            var line = new PurchaseOrderLine
            {
                PurchaseOrderId = orderId,
                ProductId = productId,
                QuantityOrdered = quantity,
                QuantityReceived = 0,
                PurchasePrice = price
            };

            _context.PurchaseOrderLines.Add(line);

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateException ex) when (IsDuplicateKey(ex))
            {
                throw new InvalidOperationException(AlreadyInOrderMessage);
            }

            return line;
        }

        //---------------------------------------------------------------------------------------------------------//

        public async Task<bool> UpdateLineAsync(int lineId, int quantity, decimal purchasePrice)
        {
            var line = await _context.PurchaseOrderLines
                .Include(l => l.PurchaseOrder)
                .FirstOrDefaultAsync(l => l.Id == lineId);

            if (line == null)
            {
                return false;
            }

            RequireDraft(line.PurchaseOrder);
            RequireValidQuantity(quantity);
            RequireValidPrice(purchasePrice);

            line.QuantityOrdered = quantity;
            line.PurchasePrice = purchasePrice;

            await _context.SaveChangesAsync();

            return true;
        }

        //---------------------------------------------------------------------------------------------------------//

        public async Task<bool> RemoveLineAsync(int lineId)
        {
            var line = await _context.PurchaseOrderLines
                .Include(l => l.PurchaseOrder)
                .FirstOrDefaultAsync(l => l.Id == lineId);

            if (line == null)
            {
                return false;
            }

            RequireDraft(line.PurchaseOrder);

            _context.PurchaseOrderLines.Remove(line);

            await _context.SaveChangesAsync();

            return true;
        }

        // --------------------------------------------------------------- Status

        // Entwurf -> Bestellt. Gibt false zurück, wenn die Bestellung nicht existiert.
        public async Task<bool> PlaceOrderAsync(int orderId)
        {
            var order = await _context.PurchaseOrders
                .Include(o => o.Lines)
                .FirstOrDefaultAsync(o => o.Id == orderId);

            if (order == null)
            {
                return false;
            }

            RequireDraft(order);

            if (order.Lines.Count == 0)
            {
                throw new InvalidOperationException(
                    "Eine Bestellung ohne Positionen kann nicht aufgegeben werden.");
            }

            order.Status = PurchaseOrderStatus.Ordered;
            order.OrderedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            return true;
        }

        //---------------------------------------------------------------------------------------------------------//

        // Möglich bei Entwurf oder Bestellt, solange nichts geliefert wurde.
        public async Task<bool> CancelAsync(int orderId)
        {
            var order = await _context.PurchaseOrders
                .Include(o => o.Lines)
                .FirstOrDefaultAsync(o => o.Id == orderId);

            if (order == null)
            {
                return false;
            }

            if (order.Status == PurchaseOrderStatus.Cancelled)
            {
                throw new InvalidOperationException("Die Bestellung ist bereits storniert.");
            }

            if (order.Status is not (PurchaseOrderStatus.Draft or PurchaseOrderStatus.Ordered)
                || order.Lines.Any(l => l.QuantityReceived > 0))
            {
                throw new InvalidOperationException(
                    "Eine Bestellung mit Wareneingang kann nicht storniert werden.");
            }

            order.Status = PurchaseOrderStatus.Cancelled;

            await _context.SaveChangesAsync();

            return true;
        }
        //---------------------------------------------------------------------------------------------------------//

        // Teilweise gelieferte Bestellung abschließen: Die offene Restmenge wird nicht mehr erwartet.
        // Gibt false zurück, wenn die Bestellung nicht existiert.
        public async Task<bool> CloseAsync(int orderId)
        {
            var order = await _context.PurchaseOrders
                .FirstOrDefaultAsync(o => o.Id == orderId);

            if (order == null)
            {
                return false;
            }

            if (order.Status != PurchaseOrderStatus.PartiallyReceived)
            {
                throw new InvalidOperationException(order.Status switch
                {
                    PurchaseOrderStatus.Closed =>
                        "Die Bestellung ist bereits abgeschlossen.",
                    PurchaseOrderStatus.Received =>
                        "Die Bestellung ist vollständig geliefert und muss nicht abgeschlossen werden.",
                    PurchaseOrderStatus.Cancelled =>
                        "Eine stornierte Bestellung kann nicht abgeschlossen werden.",
                    _ =>
                        "Nur eine teilweise gelieferte Bestellung kann abgeschlossen werden. " +
                        "Eine Bestellung ohne Wareneingang können Sie stornieren."
                });
            }

            order.Status = PurchaseOrderStatus.Closed;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                throw new InvalidOperationException(
                    "Die Bestellung wurde zwischenzeitlich geändert. Bitte laden Sie die Seite neu und prüfen Sie den aktuellen Stand.");
            }

            return true;
        }
        //---------------------------------------------------------------------------------------------------------//

        // Aktive Produkte, die noch nicht in dieser Bestellung stehen (für die Auswahl beim Hinzufügen).
        public async Task<List<ProductChoice>> GetAvailableProductsAsync(int orderId)
        {
            var usedProductIds = _context.PurchaseOrderLines
                .Where(l => l.PurchaseOrderId == orderId)
                .Select(l => l.ProductId);

            return await _context.Products
                .AsNoTracking()
                .Where(p => !p.IsArchived && !usedProductIds.Contains(p.Id))
                .OrderBy(p => p.Name)
                .Select(p => new ProductChoice
                {
                    Id = p.Id,
                    Reference = p.Reference,
                    Name = p.Name,
                    UnitPrice = p.UnitPrice
                })
                .ToListAsync();
        }
        // ------------------------------------------------------------ Wareneingang

        // Bereitet das Formular vor. Gibt null zurück, wenn die Bestellung nicht existiert
        // oder keinen Wareneingang mehr erlaubt. Es erscheinen nur Positionen mit offener Restmenge.
        public async Task<ReceiveOrderViewModel?> GetReceiptFormAsync(int orderId)
        {
            var order = await _context.PurchaseOrders
                .AsNoTracking()
                .Include(o => o.Supplier)
                .Include(o => o.Lines).ThenInclude(l => l.Product)
                .FirstOrDefaultAsync(o => o.Id == orderId &&
                    (o.Status == PurchaseOrderStatus.Ordered ||
                     o.Status == PurchaseOrderStatus.PartiallyReceived));

            if (order == null)
            {
                return null;
            }

            return new ReceiveOrderViewModel
            {
                OrderId = order.Id,
                OrderNumber = order.OrderNumber,
                SupplierName = order.Supplier.Name,
                Lines = order.Lines
                    .Where(l => l.QuantityReceived < l.QuantityOrdered)
                    .OrderBy(l => l.Product.Name)
                    .Select(l => new ReceiveLineInput
                    {
                        LineId = l.Id,
                        ProductReference = l.Product.Reference,
                        ProductName = l.Product.Name,
                        QuantityOrdered = l.QuantityOrdered,
                        QuantityReceived = l.QuantityReceived,
                        QuantityNow = l.QuantityOrdered - l.QuantityReceived
                    })
                    .ToList()
            };
        }

        //---------------------------------------------------------------------------------------------------------//

        // Bucht einen Wareneingang: erhöht Bestand und Liefermenge, schreibt je Position eine
        // Bestandsbewegung (Wareneingang) und aktualisiert den Status, alles in EINER Transaktion.
        // Gibt null zurück, wenn die Bestellung nicht existiert.
        // Wirft InvalidOperationException bei verletzten Geschäftsregeln.
        public async Task<ReceiptResult?> ReceiveAsync(int orderId, ReceiveOrderViewModel input)
        {
            var order = await _context.PurchaseOrders
                .Include(o => o.Lines).ThenInclude(l => l.Product)
                .FirstOrDefaultAsync(o => o.Id == orderId);

            if (order == null)
            {
                return null;
            }

            if (order.Status is not (PurchaseOrderStatus.Ordered or PurchaseOrderStatus.PartiallyReceived))
            {
                throw new InvalidOperationException(
                    $"Auf eine Bestellung mit dem Status „{order.Status.GetDisplayName()}“ kann kein Wareneingang gebucht werden.");
            }

            var userComment = Clean(input.Comment);

            if (userComment != null && userComment.Length > MaxReceiptCommentLength)
            {
                throw new InvalidOperationException(
                    $"Der Kommentar darf maximal {MaxReceiptCommentLength} Zeichen lang sein.");
            }

            if (input.Lines.GroupBy(l => l.LineId).Any(g => g.Count() > 1))
            {
                throw new InvalidOperationException("Eine Position wurde mehrfach übergeben.");
            }

            var requested = input.Lines
                .Where(l => l.QuantityNow.GetValueOrDefault() != 0)
                .ToList();

            if (requested.Count == 0)
            {
                throw new InvalidOperationException(
                    "Bitte geben Sie bei mindestens einer Position eine Menge ein.");
            }

            // 1. Alles prüfen, bevor irgendetwas verändert wird.
            var receipts = new List<(PurchaseOrderLine Line, int Quantity)>();

            foreach (var item in requested)
            {
                var quantity = item.QuantityNow.GetValueOrDefault();
                var line = order.Lines.FirstOrDefault(l => l.Id == item.LineId);

                if (line == null)
                {
                    throw new InvalidOperationException("Die Position gehört nicht zu dieser Bestellung.");
                }

                if (quantity < 0)
                {
                    throw new InvalidOperationException("Die Menge darf nicht negativ sein.");
                }

                var remaining = line.QuantityOrdered - line.QuantityReceived;

                if (quantity > remaining)
                {
                    throw new InvalidOperationException(
                        $"Für „{line.Product.Name}“ können höchstens noch {remaining} Stück gebucht werden.");
                }

                if (line.Product.IsArchived)
                {
                    throw new InvalidOperationException(
                        $"Das Produkt „{line.Product.Name}“ ist archiviert. Ein Wareneingang ist nicht möglich.");
                }

                if (line.Product.Quantity > int.MaxValue - quantity)
                {
                    throw new InvalidOperationException("Die Menge ist zu groß.");
                }

                receipts.Add((line, quantity));
            }

            // 2. Alles anwenden (nur im Speicher), dann EIN SaveChangesAsync.
            var now = DateTime.UtcNow;

            var movementComment = userComment == null
                ? $"Wareneingang {order.OrderNumber}"
                : $"Wareneingang {order.OrderNumber}: {userComment}";

            foreach (var (line, quantity) in receipts)
            {
                line.QuantityReceived += quantity;
                line.Product.Quantity += quantity;

                _context.StockMovements.Add(new StockMovement
                {
                    ProductId = line.ProductId,
                    MovementType = StockMovementType.Entry,
                    QuantityChange = quantity,
                    QuantityAfter = line.Product.Quantity,
                    Comment = movementComment,
                    PurchaseOrderLineId = line.Id,
                    CreatedAt = now
                });
            }

            order.LastReceiptAt = now;
            order.Status = order.Lines.All(l => l.QuantityReceived >= l.QuantityOrdered)
                ? PurchaseOrderStatus.Received
                : PurchaseOrderStatus.PartiallyReceived;

            try
            {
                await _context.SaveChangesAsync();
            }
            catch (DbUpdateConcurrencyException)
            {
                throw new InvalidOperationException(
                    "Die Bestellung oder der Bestand wurde zwischenzeitlich geändert. Bitte laden Sie die Seite neu und prüfen Sie den aktuellen Stand.");
            }
            catch (DbUpdateException)
            {
                throw new InvalidOperationException(
                    "Der Wareneingang konnte nicht gespeichert werden. Bitte prüfen Sie die Mengen.");
            }

            return new ReceiptResult(order.Status, receipts.Sum(r => r.Quantity), receipts.Count);
        }

        // ------------------------------------------------------------ Hilfsmethoden

        private async Task EnsureActiveSupplierAsync(int supplierId)
        {
            var usable = await _context.Suppliers
                .AnyAsync(s => s.Id == supplierId && !s.IsArchived);

            if (!usable)
            {
                throw new InvalidOperationException(SupplierUnusableMessage);
            }
        }

        private static void RequireDraft(PurchaseOrder order)
        {
            if (order.Status != PurchaseOrderStatus.Draft)
            {
                throw new InvalidOperationException(
                    $"Die Bestellung hat den Status „{order.Status.GetDisplayName()}“ und kann nicht mehr geändert werden.");
            }
        }

        private static void RequireValidQuantity(int quantity)
        {
            if (quantity < 1)
            {
                throw new InvalidOperationException("Die Menge muss mindestens 1 betragen.");
            }
        }

        private static void RequireValidPrice(decimal price)
        {
            if (price < 0)
            {
                throw new InvalidOperationException("Der Preis darf nicht negativ sein.");
            }
        }

        // Format: B-2026-00042 (Jahr + fortlaufende Nummer). Die fünfstellige Nummer
        // macht die alphabetische Sortierung gleich der numerischen.
        private async Task<string> NextOrderNumberAsync(int year)
        {
            var prefix = $"B-{year}-";

            var last = await _context.PurchaseOrders
                .Where(o => o.OrderNumber.StartsWith(prefix))
                .OrderByDescending(o => o.OrderNumber)
                .Select(o => o.OrderNumber)
                .FirstOrDefaultAsync();

            var next = 1;

            if (last != null && int.TryParse(last.Substring(prefix.Length), out var lastNumber))
            {
                next = lastNumber + 1;
            }

            return $"{prefix}{next:D5}";
        }

        private static bool IsDuplicateKey(DbUpdateException ex) =>
            ex.InnerException is SqlException sql && (sql.Number == 2601 || sql.Number == 2627);

        private static string? Clean(string? value) =>
            string.IsNullOrWhiteSpace(value) ? null : value.Trim();
    }
}