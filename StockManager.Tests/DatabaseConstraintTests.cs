using Microsoft.EntityFrameworkCore;

namespace StockManager.Tests
{
    public class DatabaseConstraintTests
    {
        [Fact]
        public void Database_RejectsNegativeQuantity()
        {
            using var db = new TestDatabase();
            var productId = db.AddProduct(quantity: 5);

            using var context = db.CreateContext();
            var product = context.Products.Single(p => p.Id == productId);
            product.Quantity = -1;

            Assert.Throws<DbUpdateException>(() => context.SaveChanges());
        }
    }
}