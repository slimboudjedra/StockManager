using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using StockManager.Data;
using StockManager.Models;

namespace StockManager.Tests
{
    // Eine frische SQLite-Datenbank im Arbeitsspeicher pro Test.
    // Die Datenbank lebt, solange die Verbindung offen ist.
    public sealed class TestDatabase : IDisposable
    {
        private readonly SqliteConnection _connection;
        private readonly DbContextOptions<ApplicationDbContext> _options;

        public TestDatabase()
        {
            _connection = new SqliteConnection("DataSource=:memory:");
            _connection.Open();

            _options = new DbContextOptionsBuilder<ApplicationDbContext>()
                .UseSqlite(_connection)
                .Options;

            using var context = CreateContext();
            context.Database.EnsureCreated();
        }

        public ApplicationDbContext CreateContext()
        {
            return new TestApplicationDbContext(_options);
        }

        // Legt ein Produkt (mit eigener Kategorie) an und gibt die Id zurück.
        public int AddProduct
            (int quantity,bool isArchived = false,int minimumStock = 0,decimal unitPrice = 1m)
        {
            using var context = CreateContext();

            var product = new Product
            {
                Reference = "REF-" + Guid.NewGuid().ToString("N")[..8],
                Name = "Testprodukt",
                Quantity = quantity,
                MinimumStock = minimumStock,
                UnitPrice = unitPrice,
                IsArchived = isArchived,
                Category = new Category { Name = "Kat-" + Guid.NewGuid().ToString("N") }
            };

            context.Products.Add(product);
            context.SaveChanges();

            return product.Id;
        }

        public int GetQuantity(int productId)
        {
            using var context = CreateContext();

            return context.Products
                .Where(p => p.Id == productId)
                .Select(p => p.Quantity)
                .Single();
        }

        public int CountMovements(int productId)
        {
            using var context = CreateContext();

            return context.StockMovements.Count(m => m.ProductId == productId);
        }

        public void Dispose()
        {
            _connection.Dispose();
        }
    }

    // SQLite kennt "rowversion" nicht. Im Test behandeln wir RowVersion deshalb
    // als gewöhnlichen Wert. Der Schutz vor gleichzeitigen Änderungen ist eine
    // Funktion von SQL Server und wird hier nicht getestet.
    internal class TestApplicationDbContext : ApplicationDbContext
    {
        public TestApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<Product>()
                .Property(p => p.RowVersion)
                .ValueGeneratedNever();
        }
    }
}