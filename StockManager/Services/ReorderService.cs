using Microsoft.EntityFrameworkCore;
using StockManager.Data;
using StockManager.Models;
using StockManager.ViewModels;

namespace StockManager.Services
{
    public class ReorderService
    {
        private const string DraftNote = "Automatischer Nachbestellvorschlag";

        private readonly ApplicationDbContext _context;
        private readonly PurchaseOrderService _orders;

        public ReorderService(ApplicationDbContext context, PurchaseOrderService orders)
        {
            _context = context;
            _orders = orders;
        }

        //---------------------------------------------------------------------------------------------------------//

        public async Task<ReorderSuggestionsViewModel> GetSuggestionsAsync()
        {
            var openStatuses = new[]
            {
                PurchaseOrderStatus.Draft,
                PurchaseOrderStatus.Ordered,
                PurchaseOrderStatus.PartiallyReceived
            };

            // Noch erwartete Mengen je Produkt (Entwürfe zählen als "geplant")
            var incoming = await _context.PurchaseOrderLines
                .AsNoTracking()
                .Where(l => openStatuses.Contains(l.PurchaseOrder.Status))
                .GroupBy(l => l.ProductId)
                .Select(g => new
                {
                    ProductId = g.Key,
                    Open = g.Sum(l => l.QuantityOrdered - l.QuantityReceived)
                })
                .ToDictionaryAsync(x => x.ProductId, x => x.Open);

            var candidates = await _context.Products
                .AsNoTracking()
                .Where(p => !p.IsArchived && p.MinimumStock > 0 && p.Quantity <= p.MinimumStock)
                .Select(p => new
                {
                    p.Id,
                    p.Reference,
                    p.Name,
                    p.Quantity,
                    p.MinimumStock,
                    p.UnitPrice,
                    SupplierId = p.PreferredSupplierId,
                    SupplierName = p.PreferredSupplier != null ? p.PreferredSupplier.Name : null,
                    SupplierArchived = p.PreferredSupplier != null && p.PreferredSupplier.IsArchived
                })
                .ToListAsync();

            var entries = new List<(ReorderItem Item, int? SupplierId, string? SupplierName, bool SupplierArchived)>();

            foreach (var p in candidates)
            {
                var open = incoming.TryGetValue(p.Id, out var value) ? value : 0;
                var effective = (long)p.Quantity + open;

                // Schon durch bestellte oder geplante Mengen gedeckt
                if (effective > p.MinimumStock)
                {
                    continue;
                }

                var target = (long)p.MinimumStock * 2;
                var suggested = (int)Math.Min(target - effective, int.MaxValue);

                if (suggested < 1)
                {
                    continue;
                }

                var item = new ReorderItem
                {
                    ProductId = p.Id,
                    Reference = p.Reference,
                    Name = p.Name,
                    Quantity = p.Quantity,
                    MinimumStock = p.MinimumStock,
                    OpenQuantity = open,
                    SuggestedQuantity = suggested,
                    UnitPrice = p.UnitPrice
                };

                entries.Add((item, p.SupplierId, p.SupplierName, p.SupplierArchived));
            }

            var groups = entries
                .Where(e => e.SupplierId != null && !e.SupplierArchived)
                .GroupBy(e => new { Id = e.SupplierId!.Value, Name = e.SupplierName! })
                .OrderBy(g => g.Key.Name)
                .Select(g => new ReorderSupplierGroup
                {
                    SupplierId = g.Key.Id,
                    SupplierName = g.Key.Name,
                    Items = g.Select(e => e.Item).OrderBy(i => i.Name).ToList()
                })
                .ToList();

            var withoutSupplier = entries
                .Where(e => e.SupplierId == null || e.SupplierArchived)
                .Select(e => e.Item)
                .OrderBy(i => i.Name)
                .ToList();

            return new ReorderSuggestionsViewModel
            {
                Groups = groups,
                WithoutSupplier = withoutSupplier
            };
        }

        //---------------------------------------------------------------------------------------------------------//

        // Legt Entwürfe an: für einen Lieferanten oder (ohne Angabe) für alle Lieferanten mit Vorschlag.
        // Jeder Entwurf wird in einem Schritt gespeichert. Danach sind die Produkte durch den
        // Entwurf gedeckt und erscheinen nicht mehr im Vorschlag, ein doppelter Klick ist also harmlos.
        // Wirft InvalidOperationException, wenn es keinen passenden Vorschlag gibt.
        public async Task<List<PurchaseOrder>> CreateDraftsAsync(int? supplierId)
        {
            var suggestions = await GetSuggestionsAsync();

            var groups = suggestions.Groups
                .Where(g => supplierId == null || g.SupplierId == supplierId.Value)
                .ToList();

            if (groups.Count == 0)
            {
                throw new InvalidOperationException(
                    "Es gibt keinen Nachbestellvorschlag mehr. Die Produkte sind inzwischen durch Entwürfe oder Bestellungen gedeckt.");
            }

            var created = new List<PurchaseOrder>();

            foreach (var group in groups)
            {
                var lines = group.Items
                    .Select(i => new DraftLineInput(i.ProductId, i.SuggestedQuantity, i.UnitPrice))
                    .ToList();

                var order = await _orders.CreateDraftWithLinesAsync(group.SupplierId, lines, DraftNote);

                created.Add(order);
            }

            return created;
        }
    }
}