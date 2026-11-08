using StockManager.Services;
using StockManager.ViewModels;

namespace StockManager.Tests
{
    public class ProductServiceSupplierTests
    {
        private static ProductEditViewModel NewProduct(int categoryId, int? supplierId = null) => new()
        {
            Reference = "REF-" + Guid.NewGuid().ToString("N")[..8],
            Name = "Testprodukt",
            CategoryId = categoryId,
            MinimumStock = 1,
            UnitPrice = 2.50m,
            PreferredSupplierId = supplierId
        };

        private static SupplierEditViewModel NewSupplier(string name) => new() { Name = name };

        [Fact]
        public async Task Create_WithAnActiveSupplier_StoresTheSupplier()
        {
            using var db = new TestDatabase();
            var categoryId = db.AddCategory();
            using var context = db.CreateContext();
            var suppliers = new SupplierService(context);
            var products = new ProductService(context);

            var supplier = await suppliers.CreateAsync(NewSupplier("Alpha GmbH"));

            var product = await products.CreateProductAsync(NewProduct(categoryId, supplier.Id));

            Assert.Equal(supplier.Id, product.PreferredSupplierId);

            var edit = await products.GetProductForEditAsync(product.Id);
            Assert.NotNull(edit);
            Assert.Equal(supplier.Id, edit.PreferredSupplierId);
        }

        [Fact]
        public async Task Create_WithoutASupplier_StoresNull()
        {
            using var db = new TestDatabase();
            var categoryId = db.AddCategory();
            using var context = db.CreateContext();
            var products = new ProductService(context);

            var product = await products.CreateProductAsync(NewProduct(categoryId));

            Assert.Null(product.PreferredSupplierId);
        }

        [Fact]
        public async Task Create_WithAnArchivedSupplier_IsRejected()
        {
            using var db = new TestDatabase();
            var categoryId = db.AddCategory();
            using var context = db.CreateContext();
            var suppliers = new SupplierService(context);
            var products = new ProductService(context);

            var supplier = await suppliers.CreateAsync(NewSupplier("Alpha GmbH"));
            await suppliers.ArchiveAsync(supplier.Id);

            var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
                products.CreateProductAsync(NewProduct(categoryId, supplier.Id)));

            Assert.Equal("PreferredSupplierId", ex.ParamName);
        }

        [Fact]
        public async Task Create_WithAnUnknownSupplier_IsRejected()
        {
            using var db = new TestDatabase();
            var categoryId = db.AddCategory();
            using var context = db.CreateContext();
            var products = new ProductService(context);

            var ex = await Assert.ThrowsAsync<ArgumentException>(() =>
                products.CreateProductAsync(NewProduct(categoryId, 9999)));

            Assert.Equal("PreferredSupplierId", ex.ParamName);
        }

        [Fact]
        public async Task Update_ChangesAndClearsTheSupplier()
        {
            using var db = new TestDatabase();
            var categoryId = db.AddCategory();
            using var context = db.CreateContext();
            var suppliers = new SupplierService(context);
            var products = new ProductService(context);

            var alpha = await suppliers.CreateAsync(NewSupplier("Alpha GmbH"));
            var beta = await suppliers.CreateAsync(NewSupplier("Beta AG"));
            var product = await products.CreateProductAsync(NewProduct(categoryId, alpha.Id));

            await products.UpdateProductAsync(product.Id, NewProduct(categoryId, beta.Id));
            var afterChange = await products.GetProductForEditAsync(product.Id);

            await products.UpdateProductAsync(product.Id, NewProduct(categoryId, null));
            var afterClear = await products.GetProductForEditAsync(product.Id);

            Assert.NotNull(afterChange);
            Assert.Equal(beta.Id, afterChange.PreferredSupplierId);
            Assert.NotNull(afterClear);
            Assert.Null(afterClear.PreferredSupplierId);
        }

        [Fact]
        public async Task GetProductById_IncludesTheSupplier()
        {
            using var db = new TestDatabase();
            var categoryId = db.AddCategory();

            int productId;
            using (var context = db.CreateContext())
            {
                var suppliers = new SupplierService(context);
                var products = new ProductService(context);

                var supplier = await suppliers.CreateAsync(NewSupplier("Alpha GmbH"));
                var created = await products.CreateProductAsync(NewProduct(categoryId, supplier.Id));
                productId = created.Id;
            }

            // Neuer Kontext: Der Lieferant darf nur geladen sein, wenn Include ihn mitbringt.
            using var freshContext = db.CreateContext();
            var loaded = await new ProductService(freshContext).GetProductByIdAsync(productId);

            Assert.NotNull(loaded);
            Assert.NotNull(loaded.PreferredSupplier);
            Assert.Equal("Alpha GmbH", loaded.PreferredSupplier.Name);
        }
    }
}