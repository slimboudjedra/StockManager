using Microsoft.EntityFrameworkCore;
using StockManager.Data;
using StockManager.Models;
using StockManager.ViewModels;

namespace StockManager.Services
{
    public record DemoSeedResult(bool Seeded, string Message);

    // Legt fiktive Beispieldaten an, und zwar nur in einer Datenbank ohne Fachdaten.
    // Alles läuft über die echten Services, die Geschäftsregeln gelten also auch hier.
    public class DemoDataSeeder
    {
        private const int DemoSpanDays = 28;

        private const string SupplierA = "Schraubenwerk Ruhr GmbH";
        private const string SupplierB = "Elektro-Großhandel Westfalen KG";
        private const string SupplierC = "Verpackung Nord AG";
        private const string SupplierD = "Werkzeug-Depot Süd GmbH";

        private const string CatFasteners = "Befestigungstechnik";
        private const string CatElectrical = "Elektrozubehör";
        private const string CatPackaging = "Verpackung";
        private const string CatSafety = "Arbeitsschutz";
        private const string CatTools = "Werkzeug";

        private readonly ApplicationDbContext _context;
        private readonly CategoryService _categories;
        private readonly SupplierService _suppliers;
        private readonly ProductService _products;
        private readonly StockService _stock;
        private readonly PurchaseOrderService _orders;

        public DemoDataSeeder(
            ApplicationDbContext context,
            CategoryService categories,
            SupplierService suppliers,
            ProductService products,
            StockService stock,
            PurchaseOrderService orders)
        {
            _context = context;
            _categories = categories;
            _suppliers = suppliers;
            _products = products;
            _stock = stock;
            _orders = orders;
        }

        // ------------------------------------------------------------ Daten

        private sealed record Step(StockMovementType Type, int Quantity, string Comment);

        private sealed record DemoProduct(
            string Reference,
            string Name,
            string Description,
            string Category,
            string? Supplier,
            int Minimum,
            decimal Price,
            Step[] Steps);

        private static Step In(int quantity, string comment) =>
            new(StockMovementType.Entry, quantity, comment);

        private static Step Out(int quantity, string comment) =>
            new(StockMovementType.Exit, quantity, comment);

        private static Step Recount(int counted, string comment) =>
            new(StockMovementType.Adjustment, counted, comment);

        private static readonly (string Name, string Description)[] CategoryDefinitions =
        {
            (CatFasteners, "Schrauben, Muttern, Scheiben und Dübel"),
            (CatElectrical, "Kabel, Leuchtmittel und Steckdosenleisten"),
            (CatPackaging, "Kartons, Folien, Klebeband und Etiketten"),
            (CatSafety, "Handschuhe und Schutzbrillen"),
            (CatTools, "Handwerkzeug und Messmittel")
        };

        private static SupplierEditViewModel[] SupplierDefinitions() => new[]
        {
            new SupplierEditViewModel
            {
                Name = SupplierA, ContactPerson = "Erika Musterfrau",
                Email = "bestellung@schraubenwerk-ruhr.example", Phone = "0231 5550100",
                Street = "Industriestraße 12", PostalCode = "44147", City = "Dortmund",
                Notes = "Lieferung in der Regel innerhalb von drei Werktagen."
            },
            new SupplierEditViewModel
            {
                Name = SupplierB, ContactPerson = "Max Mustermann",
                Email = "vertrieb@elektro-westfalen.example", Phone = "0251 5550200",
                Street = "Hafenweg 5", PostalCode = "48155", City = "Münster",
                Notes = "Mindestbestellwert 150 Euro."
            },
            new SupplierEditViewModel
            {
                Name = SupplierC, ContactPerson = "Sabine Beispiel",
                Email = "service@verpackung-nord.example", Phone = "040 5550300",
                Street = "Speicherstraße 20", PostalCode = "20457", City = "Hamburg",
                Notes = "Staffelpreise ab 500 Stück."
            },
            new SupplierEditViewModel
            {
                Name = SupplierD, ContactPerson = "Jonas Demo",
                Email = "info@werkzeug-depot.example", Phone = "089 5550400",
                Street = "Gewerbering 8", PostalCode = "80335", City = "München"
            }
        };

        private static readonly DemoProduct[] ProductDefinitions =
        {
            new("SCH-M6-30", "Sechskantschrauben M6 × 30 (100er)", "Stahl verzinkt", CatFasteners, SupplierA, 20, 6.90m,
                new[] { In(120, "Anfangsbestand"), Out(40, "Verbrauch Montage"), Out(45, "Auslieferung Kundenauftrag") }),
            new("MUT-M6", "Sechskantmuttern M6 (100er)", "Stahl verzinkt", CatFasteners, SupplierA, 20, 4.50m,
                new[] { In(80, "Anfangsbestand"), Out(50, "Verbrauch Montage"), Out(25, "Auslieferung Kundenauftrag") }),
            new("SCB-M6", "Unterlegscheiben M6 (200er)", "Form A, verzinkt", CatFasteners, SupplierA, 15, 3.20m,
                new[] { In(60, "Anfangsbestand"), Out(20, "Verbrauch Montage"), Recount(38, "Inventur: zwei Packungen fehlen") }),
            new("DUB-8", "Spreizdübel 8 mm (50er)", "Nylon, mit Schraube", CatFasteners, SupplierA, 25, 5.80m,
                new[] { In(100, "Anfangsbestand"), Out(60, "Auslieferung Kundenauftrag"), Out(25, "Verbrauch Montage") }),
            new("KAB-200", "Kabelbinder 200 mm (100er)", "Schwarz, UV-beständig", CatElectrical, SupplierB, 40, 3.90m,
                new[] { In(200, "Anfangsbestand"), Out(90, "Verbrauch Montage"), Out(60, "Auslieferung Kundenauftrag") }),
            new("LED-E27", "LED-Leuchtmittel E27, 9 W", "Warmweiß, 806 lm", CatElectrical, SupplierB, 30, 2.95m,
                new[] { In(120, "Anfangsbestand"), Out(40, "Auslieferung Kundenauftrag"), Out(35, "Verbrauch Montage") }),
            new("STE-6", "Steckdosenleiste 6-fach, 3 m", "Mit Überspannungsschutz", CatElectrical, SupplierB, 12, 11.90m,
                new[] { In(40, "Anfangsbestand"), Out(15, "Auslieferung Kundenauftrag"), Out(20, "Verbrauch Montage") }),
            new("VER-5", "Verlängerungskabel 5 m", "Dreiadrig, für den Innenbereich", CatElectrical, SupplierB, 10, 8.40m,
                new[] { In(30, "Anfangsbestand"), Out(8, "Auslieferung Kundenauftrag") }),
            new("KAR-4030", "Versandkarton 400 × 300 × 200 mm", "Wellpappe, zweiwellig", CatPackaging, SupplierC, 150, 1.15m,
                new[] { In(800, "Anfangsbestand"), Out(300, "Versand Kundenaufträge"), Out(250, "Versand Kundenaufträge") }),
            new("KLB-50", "Paketklebeband 50 mm × 66 m", "Transparent, leise abrollbar", CatPackaging, SupplierC, 48, 1.65m,
                new[] { In(200, "Anfangsbestand"), Out(80, "Versand Kundenaufträge"), Out(50, "Versand Kundenaufträge") }),
            new("STR-500", "Stretchfolie 500 mm, 2,3 kg", "Handrolle, transparent", CatPackaging, SupplierC, 12, 7.20m,
                new[] { In(48, "Anfangsbestand"), Out(18, "Palettensicherung") }),
            new("ETI-100", "Etikettenrolle 100 × 50 mm", "Thermodirekt, 500 Stück", CatPackaging, SupplierC, 24, 6.40m,
                new[] { In(90, "Anfangsbestand"), Out(40, "Versand Kundenaufträge"), Out(35, "Versand Kundenaufträge") }),
            new("HAN-L", "Arbeitshandschuhe Gr. L", "Nylon, nitrilbeschichtet", CatSafety, null, 30, 3.50m,
                new[] { In(150, "Anfangsbestand"), Out(60, "Ausgabe Werkstatt"), Out(70, "Ausgabe Werkstatt") }),
            new("SCH-BRI", "Schutzbrille klar", "Kratzfest, beschlagfrei", CatSafety, null, 15, 4.20m,
                new[] { In(40, "Anfangsbestand"), Out(15, "Ausgabe Werkstatt") }),
            new("WZ-AKKU", "Akkuschrauber 18 V", "Mit zwei Akkus und Ladegerät", CatTools, SupplierD, 4, 89.00m,
                new[] { In(10, "Anfangsbestand"), Out(3, "Auslieferung Kundenauftrag"), Out(2, "Ausgabe Werkstatt") }),
            new("WZ-MASS", "Maßband 5 m", "Stahlband, beidseitig bedruckt", CatTools, SupplierD, 10, 4.90m,
                new[] { In(25, "Anfangsbestand"), Out(10, "Ausgabe Werkstatt"), Out(8, "Ausgabe Werkstatt") })
        };

        // ---------------------------------------------------------- Ablauf

        public async Task<DemoSeedResult> SeedAsync()
        {
            var hasBusinessData = await _context.Products.AnyAsync()
                || await _context.Categories.AnyAsync()
                || await _context.Suppliers.AnyAsync()
                || await _context.PurchaseOrders.AnyAsync();

            if (hasBusinessData)
            {
                return new DemoSeedResult(false, "Übersprungen: Die Datenbank enthält bereits Fachdaten.");
            }

            var start = DateTime.UtcNow;

            var categoryIds = await CreateCategoriesAsync();
            var supplierIds = await CreateSuppliersAsync();
            var productIds = await CreateProductsAsync(categoryIds, supplierIds);

            await BookStockAsync(productIds);
            await CreateOrdersAsync(supplierIds, productIds);
            await BookTailMovementsAsync(productIds);

            await SpreadTimestampsAsync(start, DateTime.UtcNow);

            var orderCount = await _context.PurchaseOrders.CountAsync();
            var movementCount = await _context.StockMovements.CountAsync();

            return new DemoSeedResult(
                true,
                $"Demodaten angelegt: {ProductDefinitions.Length} Produkte, {CategoryDefinitions.Length} Kategorien, " +
                $"{supplierIds.Count} aktive Lieferanten (und einer archiviert), {orderCount} Bestellungen, {movementCount} Bewegungen.");
        }

        private async Task<Dictionary<string, int>> CreateCategoriesAsync()
        {
            var ids = new Dictionary<string, int>();

            foreach (var (name, description) in CategoryDefinitions)
            {
                var category = await _categories.CreateAsync(new Category { Name = name, Description = description });
                ids[name] = category.Id;
            }

            return ids;
        }

        private async Task<Dictionary<string, int>> CreateSuppliersAsync()
        {
            var ids = new Dictionary<string, int>();

            foreach (var input in SupplierDefinitions())
            {
                var supplier = await _suppliers.CreateAsync(input);
                ids[supplier.Name] = supplier.Id;
            }

            // Ein archivierter Lieferant, damit auch der Reiter "Archiviert" etwas zeigt.
            var retired = await _suppliers.CreateAsync(new SupplierEditViewModel
            {
                Name = "Altlieferant Beispiel GmbH",
                ContactPerson = "Erwin Altmann",
                City = "Bochum",
                Notes = "Geschäftsbeziehung beendet."
            });
            await _suppliers.ArchiveAsync(retired.Id);

            return ids;
        }

        private async Task<Dictionary<string, int>> CreateProductsAsync(
            Dictionary<string, int> categoryIds, Dictionary<string, int> supplierIds)
        {
            var ids = new Dictionary<string, int>();

            foreach (var definition in ProductDefinitions)
            {
                var product = await _products.CreateProductAsync(new ProductEditViewModel
                {
                    Reference = definition.Reference,
                    Name = definition.Name,
                    Description = definition.Description,
                    CategoryId = categoryIds[definition.Category],
                    PreferredSupplierId = definition.Supplier == null ? null : supplierIds[definition.Supplier],
                    MinimumStock = definition.Minimum,
                    UnitPrice = definition.Price
                });

                ids[definition.Reference] = product.Id;
            }

            return ids;
        }

        // Reihum: erst alle Anfangsbestände, dann alle ersten Entnahmen und so weiter.
        // So mischen sich die Bewegungen der Produkte in der Historie.
        private async Task BookStockAsync(Dictionary<string, int> productIds)
        {
            var maxSteps = ProductDefinitions.Max(d => d.Steps.Length);

            for (var i = 0; i < maxSteps; i++)
            {
                foreach (var definition in ProductDefinitions)
                {
                    if (i < definition.Steps.Length)
                    {
                        await RecordAsync(productIds[definition.Reference], definition.Steps[i]);
                    }
                }
            }
        }

        private async Task CreateOrdersAsync(
            Dictionary<string, int> suppliers, Dictionary<string, int> products)
        {
            // 1. Vollständig geliefert
            var received = await CreateDraftAsync(suppliers[SupplierA], "Quartalsbestellung Befestigungstechnik", null,
                (products["SCH-M6-30"], 50, 6.50m), (products["SCB-M6"], 20, 3.00m));
            await PlaceAsync(received.Order);
            await ReceiveAsync(received.Order, "Lieferschein 2026-0412",
                (received.Lines[0], 50), (received.Lines[1], 20));

            // 2. Teilweise geliefert
            var partial = await CreateDraftAsync(suppliers[SupplierB], "Elektrozubehör für den Winter", 2,
                (products["KAB-200"], 100, 3.70m), (products["LED-E27"], 60, 2.80m));
            await PlaceAsync(partial.Order);
            await ReceiveAsync(partial.Order, "Teillieferung, Rest folgt",
                (partial.Lines[0], 60));

            // 3. Bestellt, noch nichts geliefert
            var placed = await CreateDraftAsync(suppliers[SupplierC], "Verpackungsmaterial Nachschub", 5,
                (products["KLB-50"], 96, 1.55m), (products["STR-500"], 24, 6.90m));
            await PlaceAsync(placed.Order);

            // 4. Teilweise geliefert und dann abgeschlossen
            var closed = await CreateDraftAsync(suppliers[SupplierD], "Messmittel", null,
                (products["WZ-MASS"], 20, 4.60m));
            await PlaceAsync(closed.Order);
            await ReceiveAsync(closed.Order, "Rest nicht mehr lieferbar",
                (closed.Lines[0], 12));
            await _orders.CloseAsync(closed.Order.Id);

            // 5. Storniert
            var cancelled = await CreateDraftAsync(suppliers[SupplierA], "Bestellung ersetzt durch Sammelbestellung", null,
                (products["DUB-8"], 50, 5.50m));
            await _orders.CancelAsync(cancelled.Order.Id);

            // 6. Entwurf
            await CreateDraftAsync(suppliers[SupplierB], "Entwurf für die nächste Bestellrunde", 7,
                (products["STE-6"], 20, 11.20m), (products["VER-5"], 10, 8.00m));
        }

        private async Task BookTailMovementsAsync(Dictionary<string, int> products)
        {
            await RecordAsync(products["KAR-4030"], Out(25, "Versand Kundenaufträge"));
            await RecordAsync(products["LED-E27"], Out(5, "Verbrauch Montage"));
            await RecordAsync(products["SCH-BRI"], Out(5, "Ausgabe Werkstatt"));
        }

        // ------------------------------------------------------ Hilfsmethoden

        private async Task RecordAsync(int productId, Step step)
        {
            var movement = await _stock.RecordMovementAsync(productId, step.Type, step.Quantity, step.Comment);

            if (movement == null)
            {
                throw new InvalidOperationException("Demodaten: Produkt nicht gefunden.");
            }
        }

        private async Task<(PurchaseOrder Order, List<PurchaseOrderLine> Lines)> CreateDraftAsync(
            int supplierId,
            string notes,
            int? expectedInDays,
            params (int ProductId, int Quantity, decimal Price)[] lines)
        {
            var order = await _orders.CreateDraftAsync(new PurchaseOrderEditViewModel
            {
                SupplierId = supplierId,
                Notes = notes,
                ExpectedDeliveryDate = expectedInDays == null
                    ? null
                    : DateOnly.FromDateTime(DateTime.UtcNow).AddDays(expectedInDays.Value)
            });

            var created = new List<PurchaseOrderLine>();

            foreach (var line in lines)
            {
                var added = await _orders.AddLineAsync(order.Id, line.ProductId, line.Quantity, line.Price);

                if (added == null)
                {
                    throw new InvalidOperationException("Demodaten: Bestellung nicht gefunden.");
                }

                created.Add(added);
            }

            return (order, created);
        }

        private async Task PlaceAsync(PurchaseOrder order)
        {
            if (!await _orders.PlaceOrderAsync(order.Id))
            {
                throw new InvalidOperationException("Demodaten: Bestellung nicht gefunden.");
            }
        }

        private async Task ReceiveAsync(
            PurchaseOrder order, string comment, params (PurchaseOrderLine Line, int Quantity)[] receipts)
        {
            var result = await _orders.ReceiveAsync(order.Id, new ReceiveOrderViewModel
            {
                OrderId = order.Id,
                Comment = comment,
                Lines = receipts
                    .Select(r => new ReceiveLineInput { LineId = r.Line.Id, QuantityNow = r.Quantity })
                    .ToList()
            });

            if (result == null)
            {
                throw new InvalidOperationException("Demodaten: Bestellung nicht gefunden.");
            }
        }

        // Alles entsteht in wenigen Sekunden. Diese Zeitspanne wird gleichmäßig auf die letzten
        // 28 Tage gedehnt. Die Reihenfolge aller Ereignisse bleibt dabei erhalten.
        private async Task SpreadTimestampsAsync(DateTime start, DateTime end)
        {
            var span = end - start;

            if (span <= TimeSpan.Zero)
            {
                return;
            }

            var target = TimeSpan.FromDays(DemoSpanDays);
            var origin = end - target;
            var ratio = (double)target.Ticks / span.Ticks;

            DateTime Map(DateTime value) => origin.AddTicks((long)((value - start).Ticks * ratio));
            DateTime? MapOrNull(DateTime? value) => value.HasValue ? Map(value.Value) : null;

            foreach (var product in await _context.Products.ToListAsync())
            {
                product.CreatedAt = Map(product.CreatedAt);
            }

            foreach (var supplier in await _context.Suppliers.ToListAsync())
            {
                supplier.CreatedAt = Map(supplier.CreatedAt);
            }

            foreach (var movement in await _context.StockMovements.ToListAsync())
            {
                movement.CreatedAt = Map(movement.CreatedAt);
            }

            foreach (var order in await _context.PurchaseOrders.ToListAsync())
            {
                order.CreatedAt = Map(order.CreatedAt);
                order.OrderedAt = MapOrNull(order.OrderedAt);
                order.LastReceiptAt = MapOrNull(order.LastReceiptAt);
            }

            await _context.SaveChangesAsync();
        }
    }
}