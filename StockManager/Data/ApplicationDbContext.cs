using Microsoft.EntityFrameworkCore;
using StockManager.Models;

namespace StockManager.Data
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options) : base(options)
        {

        }

        public DbSet<Product> Products { get; set; } = null!;

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            // Configure the Product entity
            modelBuilder.Entity<Product>().HasIndex(p => p.Reference).IsUnique();
        }
    }
}
