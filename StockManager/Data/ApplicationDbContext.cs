using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using StockManager.Models;


namespace StockManager.Data
{
    public class ApplicationDbContext : IdentityDbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options)
        {

        }

        public DbSet<Product> Products { get; set; } = null!;
        public DbSet<Category> Categories { get; set; } = null!;
        public DbSet<StockMovement> StockMovements { get; set; } = null!;
        public DbSet<Supplier> Suppliers { get; set; } = null!;
        public DbSet<PurchaseOrder> PurchaseOrders { get; set; } = null!;
        public DbSet<PurchaseOrderLine> PurchaseOrderLines { get; set; } = null!;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Entity<Product>(entity =>
            {
                entity.HasIndex(p => p.Reference)
                    .IsUnique()
                    .HasFilter("[IsArchived] = 0");

                entity.HasOne(p => p.Category)
                    .WithMany(c => c.Products)
                    .HasForeignKey(p => p.CategoryId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.ToTable(t => t.HasCheckConstraint(
                    "CK_Products_Quantity_NonNegative",
                    "[Quantity] >= 0"));
                entity.HasOne(p => p.PreferredSupplier)
                    .WithMany()
                    .HasForeignKey(p => p.PreferredSupplierId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<Category>(entity =>
            {
                entity.ToTable("Categories");

                entity.HasIndex(c => c.Name)
                    .IsUnique();
            });

            modelBuilder.Entity<StockMovement>(entity =>
            {
                entity.HasOne(m => m.Product)
                    .WithMany()
                    .HasForeignKey(m => m.ProductId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasOne(m => m.PurchaseOrderLine)
                    .WithMany()
                    .HasForeignKey(m => m.PurchaseOrderLineId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasIndex(m => new { m.ProductId, m.CreatedAt });
            });
            modelBuilder.Entity<Supplier>(entity =>
            {
                    entity.HasIndex(s => s.Name)
                          .IsUnique()
                          .HasFilter("[IsArchived] = 0");
            });
            modelBuilder.Entity<PurchaseOrder>(entity =>
            {
                entity.HasIndex(o => o.OrderNumber)
                    .IsUnique();

                entity.HasOne(o => o.Supplier)
                    .WithMany()
                    .HasForeignKey(o => o.SupplierId)
                    .OnDelete(DeleteBehavior.Restrict);
            });

            modelBuilder.Entity<PurchaseOrderLine>(entity =>
            {
                entity.HasOne(l => l.PurchaseOrder)
                    .WithMany(o => o.Lines)
                    .HasForeignKey(l => l.PurchaseOrderId)
                    .OnDelete(DeleteBehavior.Cascade);

                entity.HasOne(l => l.Product)
                    .WithMany()
                    .HasForeignKey(l => l.ProductId)
                    .OnDelete(DeleteBehavior.Restrict);

                entity.HasIndex(l => new { l.PurchaseOrderId, l.ProductId })
                    .IsUnique();

                entity.ToTable(t =>
                {
                    t.HasCheckConstraint(
                        "CK_PurchaseOrderLines_QuantityOrdered",
                        "[QuantityOrdered] >= 1");

                    t.HasCheckConstraint(
                        "CK_PurchaseOrderLines_QuantityReceived",
                        "[QuantityReceived] >= 0 AND [QuantityReceived] <= [QuantityOrdered]");
                });
            });
        }
    }
}
